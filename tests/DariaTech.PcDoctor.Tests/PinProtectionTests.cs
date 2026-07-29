using DariaTech.PcDoctor.Core.Security;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

/// <summary>
/// Prüft den Zugangsschutz. Diese Logik entscheidet, wer die Anwendung bedienen
/// darf – ein Fehler hier wäre entweder eine offene Tür oder eine Aussperrung
/// des eigenen Technikers.
/// </summary>
public class PinHasherTests
{
    // Bewusst niedrige Rundenzahl NUR im Test, damit die Läufe schnell bleiben.
    private const int TestIterations = 1_000;

    [Fact]
    public void Verify_CorrectPin_IsAccepted()
    {
        var (salt, hash, _) = PinHasher.CreateSecret("12345678", TestIterations);
        Assert.True(PinHasher.Verify("12345678", salt, hash, TestIterations));
    }

    [Theory]
    [InlineData("12345679")]     // eine Stelle anders
    [InlineData("1234567")]      // kürzer
    [InlineData("123456789")]    // länger
    [InlineData("")]
    [InlineData(null)]
    public void Verify_WrongPin_IsRejected(string? attempt)
    {
        var (salt, hash, _) = PinHasher.CreateSecret("12345678", TestIterations);
        Assert.False(PinHasher.Verify(attempt, salt, hash, TestIterations));
    }

    [Fact]
    public void CreateSecret_SamePinTwice_YieldsDifferentSaltAndHash()
    {
        var first = PinHasher.CreateSecret("Werkstatt-2026", TestIterations);
        var second = PinHasher.CreateSecret("Werkstatt-2026", TestIterations);

        Assert.NotEqual(first.SaltBase64, second.SaltBase64);
        Assert.NotEqual(first.HashBase64, second.HashBase64);   // Zufallssalz wirkt
        Assert.True(PinHasher.Verify("Werkstatt-2026", first.SaltBase64, first.HashBase64, TestIterations));
        Assert.True(PinHasher.Verify("Werkstatt-2026", second.SaltBase64, second.HashBase64, TestIterations));
    }

    [Fact]
    public void Verify_WrongIterationCount_IsRejected()
    {
        var (salt, hash, _) = PinHasher.CreateSecret("12345678", TestIterations);
        Assert.False(PinHasher.Verify("12345678", salt, hash, TestIterations + 1));
    }

    /// <summary>
    /// Wichtig: Fehlt oder ist die Konfiguration beschädigt, darf NIEMALS
    /// versehentlich Zugang gewährt werden.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("keinBase64!", "auchNicht!")]
    [InlineData("AAAA", null)]
    public void Verify_MissingOrBrokenConfiguration_DeniesAccess(string? salt, string? hash)
        => Assert.False(PinHasher.Verify("12345678", salt, hash, TestIterations));

    [Fact]
    public void Derive_IsDeterministic_ForSameSalt()
    {
        var salt = PinHasher.CreateSalt();
        var a = PinHasher.Derive("12345678", salt, TestIterations);
        var b = PinHasher.Derive("12345678", salt, TestIterations);
        Assert.Equal(a, b);
        Assert.Equal(PinHasher.HashBytes, a.Length);
    }

    [Fact]
    public void CreateSalt_ProducesRandomValues()
        => Assert.NotEqual(PinHasher.CreateSalt(), PinHasher.CreateSalt());
}

public class PinPolicyTests
{
    [Theory]
    [InlineData("12345678")]
    [InlineData("Werkstatt-2026!")]
    [InlineData("87654321")]
    public void Validate_AcceptsPinsOfSufficientLength(string pin)
        => Assert.Null(PinPolicy.Validate(pin));

    [Theory]
    [InlineData("1234567")]     // 7 Zeichen -> zu kurz
    [InlineData("123")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_RejectsTooShortOrEmpty(string? pin)
        => Assert.NotNull(PinPolicy.Validate(pin));

    [Fact]
    public void Validate_MentionsMinimumLength()
        => Assert.Contains("8", PinPolicy.Validate("123")!);

    [Fact]
    public void MinimumLength_IsEight()
        => Assert.Equal(8, PinPolicy.MinimumLength);

    [Theory]
    [InlineData("11111111")]      // nur ein Zeichen
    [InlineData("12345678")]      // fortlaufend
    [InlineData("87654321")]      // fortlaufend abwärts
    public void WeaknessWarning_FlagsGuessablePins(string pin)
        => Assert.NotNull(PinPolicy.WeaknessWarning(pin));

    [Theory]
    [InlineData("Werkstatt-2026!")]
    [InlineData("40719283")]
    public void WeaknessWarning_AcceptsReasonablePins(string pin)
        => Assert.Null(PinPolicy.WeaknessWarning(pin));
}

public class PinLockoutTests
{
    private static readonly DateTime Now = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FreshState_IsNotLocked()
    {
        Assert.False(PinLockout.IsLocked(PinLockoutState.Fresh, Now));
        Assert.Equal(PinLockout.AttemptsBeforeLockout, PinLockout.AttemptsLeft(PinLockoutState.Fresh));
    }

    [Fact]
    public void FirstFailures_DoNotLockButCountDown()
    {
        var state = PinLockoutState.Fresh;
        for (var i = 1; i < PinLockout.AttemptsBeforeLockout; i++)
        {
            state = PinLockout.RegisterFailure(state, Now);
            Assert.False(PinLockout.IsLocked(state, Now));
            Assert.Equal(i, state.FailedAttempts);
        }
        Assert.Equal(1, PinLockout.AttemptsLeft(state));
    }

    [Fact]
    public void ReachingAttemptLimit_LocksInput()
    {
        var state = PinLockoutState.Fresh;
        for (var i = 0; i < PinLockout.AttemptsBeforeLockout; i++)
            state = PinLockout.RegisterFailure(state, Now);

        Assert.True(PinLockout.IsLocked(state, Now));
        Assert.True(PinLockout.RemainingLock(state, Now) > TimeSpan.Zero);
    }

    [Fact]
    public void FurtherFailures_ExtendTheLockProgressively()
    {
        var state = PinLockoutState.Fresh;
        for (var i = 0; i < PinLockout.AttemptsBeforeLockout; i++)
            state = PinLockout.RegisterFailure(state, Now);
        var firstLock = PinLockout.RemainingLock(state, Now);

        state = PinLockout.RegisterFailure(state, Now);
        var secondLock = PinLockout.RemainingLock(state, Now);

        Assert.True(secondLock > firstLock, "Die Sperre muss mit jedem Fehlversuch länger werden.");
    }

    [Fact]
    public void LockExpires_AfterItsDuration()
    {
        var state = PinLockoutState.Fresh;
        for (var i = 0; i < PinLockout.AttemptsBeforeLockout; i++)
            state = PinLockout.RegisterFailure(state, Now);

        Assert.True(PinLockout.IsLocked(state, Now));
        Assert.False(PinLockout.IsLocked(state, Now.AddHours(2)));   // später wieder frei
    }

    [Fact]
    public void SuccessfulEntry_ResetsEverything()
    {
        var state = PinLockout.RegisterFailure(PinLockoutState.Fresh, Now);
        var reset = PinLockout.RegisterSuccess();

        Assert.Equal(0, reset.FailedAttempts);
        Assert.Null(reset.LockedUntilUtc);
        Assert.NotEqual(state.FailedAttempts, reset.FailedAttempts);
    }

    [Fact]
    public void Describe_ExplainsRemainingAttemptsAndLock()
    {
        var state = PinLockout.RegisterFailure(PinLockoutState.Fresh, Now);
        Assert.Contains("Versuch", PinLockout.Describe(state, Now));

        for (var i = 1; i < PinLockout.AttemptsBeforeLockout; i++)
            state = PinLockout.RegisterFailure(state, Now);
        Assert.Contains("gesperrt", PinLockout.Describe(state, Now));
    }

    [Fact]
    public void Describe_FreshState_IsEmpty()
        => Assert.Equal(string.Empty, PinLockout.Describe(PinLockoutState.Fresh, Now));
}

public class PinSessionTests
{
    private static readonly DateTime Start = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DefaultTimeout_IsThirtyMinutes()
        => Assert.Equal(TimeSpan.FromMinutes(30), PinSession.DefaultIdleTimeout);

    [Fact]
    public void NewSession_StartsLocked()
        => Assert.False(new PinSession().IsUnlocked);

    [Fact]
    public void Unlock_MakesSessionUsable()
    {
        var session = new PinSession();
        session.Unlock(Start);
        Assert.True(session.IsUnlocked);
        Assert.False(session.IsIdleTimeoutReached(Start));
    }

    [Fact]
    public void IdleTimeout_LocksAfterThirtyMinutes()
    {
        var session = new PinSession();
        session.Unlock(Start);

        Assert.False(session.IsIdleTimeoutReached(Start.AddMinutes(29)));
        Assert.True(session.IsIdleTimeoutReached(Start.AddMinutes(30)));
    }

    [Fact]
    public void Activity_PostponesTheLock()
    {
        var session = new PinSession();
        session.Unlock(Start);

        session.RegisterActivity(Start.AddMinutes(25));           // Bedienung nach 25 Min
        Assert.False(session.IsIdleTimeoutReached(Start.AddMinutes(50)));  // 25 Min danach
        Assert.True(session.IsIdleTimeoutReached(Start.AddMinutes(56)));   // 31 Min danach
    }

    [Fact]
    public void LockIfIdleTimeoutReached_ReportsAndLocks()
    {
        var session = new PinSession();
        session.Unlock(Start);

        Assert.False(session.LockIfIdleTimeoutReached(Start.AddMinutes(10)));
        Assert.True(session.IsUnlocked);

        Assert.True(session.LockIfIdleTimeoutReached(Start.AddMinutes(31)));
        Assert.False(session.IsUnlocked);
    }

    [Fact]
    public void RemainingIdleTime_CountsDown()
    {
        var session = new PinSession();
        session.Unlock(Start);

        Assert.Equal(TimeSpan.FromMinutes(20), session.RemainingIdleTime(Start.AddMinutes(10)));
        Assert.Equal(TimeSpan.Zero, session.RemainingIdleTime(Start.AddMinutes(45)));
    }

    [Fact]
    public void LockedSession_IgnoresActivityAndHasNoTimeout()
    {
        var session = new PinSession();
        session.RegisterActivity(Start);          // ohne Entsperren wirkungslos
        Assert.False(session.IsUnlocked);
        Assert.False(session.IsIdleTimeoutReached(Start.AddHours(5)));
    }

    [Fact]
    public void CustomTimeout_IsHonoured()
    {
        var session = new PinSession(TimeSpan.FromMinutes(5));
        session.Unlock(Start);
        Assert.True(session.IsIdleTimeoutReached(Start.AddMinutes(5)));
    }
}

public class PinSecretTests
{
    /// <summary>
    /// Im Test läuft die Anwendung ohne eingebetteten PIN. Wichtig ist, dass
    /// dieser Zustand klar erkannt wird und keine Prüfung „durchrutscht“.
    /// </summary>
    [Fact]
    public void WithoutEmbeddedPin_NothingIsAccepted()
    {
        if (PinSecret.IsConfigured) return;   // signierter Build mit PIN: nicht anwendbar

        Assert.False(PinSecret.Verify("12345678"));
        Assert.False(PinSecret.Verify(""));
        Assert.False(PinSecret.Verify(null));
    }

    [Fact]
    public void Iterations_AreAtLeastTheSecureDefault()
        => Assert.True(PinSecret.Iterations >= PinHasher.DefaultIterations,
            "Die Rundenzahl darf nie unter den sicheren Standardwert fallen.");
}
