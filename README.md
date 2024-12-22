# Chart Browser for FastGH3
This is a simple tool for quickly scanning and loading custom Guitar Hero songs in [FastGH3](https://github.com/donnaken15/FastGH3).

<details>
  <summary>Screenshot (v1.4.0 Dark Mode)</summary>

  ![image](https://github.com/user-attachments/assets/cafbea4f-5ef7-4bd2-9749-ec047f1d9161)

</details>
<details>
<summary>Screenshot (v1.2.2 Legacy)</summary>
  
  ![image](https://github.com/user-attachments/assets/1c315181-e215-474d-89bd-30ce71d0a976)

</details>

## Prerequisites (v1.3.0 and up)
- Windows 10 22H2 or newer
- [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (Probably required, since v1.3.0+ is built with .NET 9)
- [FastGH3](https://github.com/donnaken15/FastGH3)

If you can't get v1.3.0 or newer to launch, then either install .NET 9 or try [v1.2.2](https://github.com/YoShibyl/FGH3ChartBrowser/releases/tag/v1.2.2) instead.

## How to use

1) Make sure that FastGH3 is installed, preferably to `C:\Program Files (x86)\FastGH3`.  If installed elsewhere on your PC, click the **Browse for FastGH3.exe** button.
2) Click the **Browse for Folder** button to select the folder containing your songs.  At the moment, it's only possible to scan one folder, unfortunately.
3) Click **Scan Songs** and be patient.  This can take somewhere between a few seconds and minute(s), depending on how many songs you have and how fast your PC is.
4) You can type in the Filter Songs bar to search by title, artist, album, genre, chart author, and even the file path of the chart.
5) Click the song you want to play, then click the big ol' **Play Song** button, and rock on!

As of v1.4.0, you can use an Xinput gamepad (such as a guitar controller) to scroll through songs and launch into the game (see below)

![image](https://github.com/user-attachments/assets/73ce51e1-e5dd-4133-8fc5-746741ccc075)

## Planned features
- Storing scan results for later
- ~~Guitar controller navigation?~~ Done (v1.4.0)

## Credits
- [donnaken15](https://github.com/donnaken15) : Maker of [FastGH3](https://github.com/donnaken15/FastGH3)
- [mdsitton](https://github.com/mdsitton) : Clone Hero developer who made [the `.sng` format](https://github.com/mdsitton/SngFileFormat)
- Neversoft / Aspyr : Makers of the original *Guitar Hero III* PC port
- [teh_supar_hackr](https://www.spriters-resource.com/pc_computer/guitarheroworldtour/sheet/193359/) (The Spriters Resource) : Ripped UI button assets from *Guitar Hero World Tour* (used in v1.4.0+)
- [amerkoleci](https://github.com/amerkoleci/Vortice.Windows) : Vortice XInput library used for controller navigation
- Everyone else whose code helped me from countless Google searches
