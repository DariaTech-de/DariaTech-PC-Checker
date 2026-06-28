# Assets

App-Icon hier ablegen:

- `app.ico` — Mehrgrößen-Icon (16/32/48/256 px) im DariaTech-Look (Navy `#0d1f3c`).

Danach in `DariaTech.PcDoctor.csproj` die Zeile aktivieren:

```xml
<ApplicationIcon>Assets\app.ico</ApplicationIcon>
```

Das Icon erscheint dann in Taskleiste, Fenster und in den Dateieigenschaften der `.exe`.
