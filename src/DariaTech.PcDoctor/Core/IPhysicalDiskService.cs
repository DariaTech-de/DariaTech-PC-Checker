using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Core;

/// <summary>Listet die physischen Datenträger des Systems (für den Klon-Assistenten).</summary>
public interface IPhysicalDiskService
{
    IReadOnlyList<PhysicalDisk> Enumerate();
}
