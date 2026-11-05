<p align="center">
  <img src="docs/logo.png" width="60%" alt="Hollywood Monsters Enhanced">
</p>

<h1 align="center">Hollywood Monsters Enhanced</h1>

Play Hollywood Monsters (Pendulo Studios, 1997; Italian FX Interactive edition, 2004) on Windows 10 and 11, with a new launcher, graphics filters and working sound settings.

The start window is the original FX one, redrawn sharper and bigger for today's screens. **Gioca** starts the game, **Options** opens the new settings, **Manuale di gioco** opens the manual and **Esci** closes it.

<p align="center">
  <img src="docs/launcher.png" width="70%" alt="The start window: the original FX artwork with Options, Gioca, Esci and the manual">
</p>

<p align="center">
  <img src="docs/intro-newsboy.png" width="32%" alt="Intro: the newsboy at night">
  <img src="docs/intro-street.png" width="32%" alt="Intro: the newsboy in the street">
  <img src="docs/intro-the-quill.png" width="32%" alt="Intro: The Quill front page about Hollywood Monsters">
</p>
<p align="center">
  <img src="docs/intro-building.png" width="32%" alt="Intro: the newspaper building">
  <img src="docs/the-quill-office.png" width="32%" alt="The office of The Quill">
  <img src="docs/sound-menu.png" width="32%" alt="The game's menu with the sound sliders">
</p>
<p align="center">
  <img src="docs/options.png" width="32%" alt="The launcher's options">
  <img src="docs/filter-comparison.png" width="32%" alt="The same picture with each graphics filter">
</p>

**The game installs from your own disc, starts on Windows 11, runs full screen or in a window with sharp or filtered pixels, and the music, sound effects and voice volumes now work.**

There may still be defects I have not found. If something does not work, please open an [issue](../../issues) and tell me what you did and what you saw, so I can fix it.

You need your own copy of the game: the CD or its .iso, Italian FX Interactive edition (2004). No game files are included here.

## How to install

1. Download `HollywoodMonstersEnhanced.zip` from [Releases](../../releases/latest) and unzip it anywhere.
2. Insert the game disc, or keep the .iso file at hand.
3. Run `Setup.exe` and choose:
   - **Install from the disc, a mounted ISO or an .iso file**: it finds the disc by itself; for an .iso press **Browse...** and pick the file. Then choose where to install the game.
   - **Add the enhancements to a game I already installed**: pick the folder that has `monsters.exe`.
4. Press **Install**. When it says Done, press **Play**, or use the **Hollywood Monsters** icon on the desktop.

The setup does not need administrator rights and does not change anything in Windows. To remove everything, delete the game folder and the desktop icon.

## What was done to make it work

- **Screen.** The game draws with DirectDraw at 640x480, which breaks on modern Windows. [cnc-ddraw](https://github.com/FunkyFr3sh/cnc-ddraw) replaces it, so the game runs full screen (4:3 or stretched) or in a window at 640x480, 960x720 or 1280x960. Alt+Enter switches between window and full screen.
- **Graphics filters.** 14 ways to scale the picture: sharp pixels as in 1997, smooth, Bicubic, Lanczos, Jinc2, AMD FSR, xBR, xBRZ, SABR, OmniScale, ScaleHQ, Sharpen, CRT TV and scanlines. They only change how the picture is scaled on screen; the game itself stays 640x480. The comparison picture above shows all of them.
- **Sound sliders.** In the game's menu the arrows next to the sliders jump straight from full to silent. The game moves a slider one step for each loop while the mouse button is held down, and today's PCs run that loop so fast that a normal click empties it. The launcher sets music, sound effects, voices and subtitles when the game starts, and remembers what you change in the game's menu for next time.
- **Game volume and mouse speed.** The launcher sets the volume of the whole game, and a mouse speed that applies only while the game runs; the Windows mouse speed comes back when it closes.
- **Saved games.** After each session the launcher copies your saved games to the `Save backups` folder, only when they changed, and keeps the last 20 copies.
- **Start window.** The original "Inizio del gioco" window, scaled for big screens, with a fade in and highlighted buttons. The trailer buttons are gone because the trailers are not installed, and "Rimuovi" became **Options**. The setup takes the picture from your own copy of the game.

## Tested

- By me, by hand: the launcher, the start window, and the sound sliders in the game.
- By script on Windows 11, with Claude moving the mouse and pressing the keys and checking every screenshot: full screen and window modes, all 14 filters, the sound sliders (measured, see below), the setup from the .iso into an empty folder, and the launcher starting the game with the chosen volumes.
- Not tested yet: installing from a real CD, other editions of the game (the original Spanish one by Dinamic Multimedia, 1997, for DOS; the FX Interactive Windows editions in other languages; a rare English one) (they should get everything except the sound sliders fix), saved-game backups with real saved games.

## Made with AI

This work was done with the help of AI: Claude (by Anthropic), running in [Hermes](https://github.com/NousResearch/hermes-agent), an AI agent on my PC, guided by me ([Korvonordico](https://github.com/Korvonordico)). I said what I wanted, tried the results and made the choices; Claude wrote the code and the scripts and ran the tests.

How it went, step by step:

1. Claude looked inside the disc image without installing anything: it is the Italian FX Interactive edition, and all its programs are 32-bit.
2. The original installer asks for administrator rights and puts the game in "Program Files", where old games can have trouble with saved games, so the game was copied by hand to a normal folder.
3. Without fixes the game switched the whole screen to 640x480 and once closed with an error. dgVoodoo2 was considered; cnc-ddraw was chosen, because it is made for old 2D DirectDraw games like this one.
4. A launcher with screen, mouse and volume settings, and a desktop icon.
5. The graphics filters: 14 of them, compared side by side in one picture. I chose Sharpen for my own game.
6. Automatic backups of the saved games.
7. The sound sliders. I noticed that they only went from full to silent. Claude measured the game's sound with a small program that reads the Windows sound meter, while a script clicked the arrows. [DSOAL](https://github.com/kcat/dsoal) was tried and changed nothing, so it was removed. The cause was how long the mouse button stays down. The places where the game keeps the volumes were then found by comparing its memory before and after each click, and the launcher now sets them.
8. The original start window, saved from the game and redrawn bigger, with Options in place of the uninstall button.
9. The setup, which installs from your own disc or .iso, tested on an empty folder.
10. This page: Claude made the pictures and the text, I chose what to show and how.

Tools used: Hermes with Claude; Hermes' computer use and Python scripts to move the mouse and press keys during the tests; PowerShell for screenshots and to open the disc image; the C# compiler that ships with Windows; cnc-ddraw with the libretro shaders; DSOAL (tried, then removed); the GitHub CLI for the page preview; and [Token Quotient](https://github.com/Korvonordico/quotient-claude-tokens), my own tool, to know what each step would cost before starting it.

## How to build

Both programs are built with the C# compiler that ships with Windows, nothing to install:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe "/out:Hollywood Monsters Launcher.exe" src\Launcher.cs
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:Setup.exe src\Setup.cs
```

## Credits

- Hollywood Monsters by Pendulo Studios; Italian edition by FX Interactive. This project is not affiliated with them.
- [cnc-ddraw](https://github.com/FunkyFr3sh/cnc-ddraw) by FunkyFr3sh (MIT license), with shaders from [libretro](https://github.com/libretro/glsl-shaders) (each under its own license).

## License

MIT for the launcher and the setup. See [LICENSE](LICENSE).
