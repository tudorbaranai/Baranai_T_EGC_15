# EGC – Elemente de Grafică pe Calculator (2026-2027)

Teme de laborator la disciplina **Elemente de Grafică pe Calculator**, USV – FIESC, Calculatoare, anul III.

- **Student:** Tudor Baranai (Baranai Tudor), grupa **3132a**
- **E-mail Classroom:** tudor.baranai@student.usv.ro
- **Tehnologii:** C#, .NET 8.0, OpenTK 4.9.4 (OpenGL 3.3 Core)

## Branch-uri (câte unul pentru fiecare temă)

Fiecare temă este pe propriul branch și conține **doar folderul proiectului** (fără soluția întreagă, fără `bin/` și `obj/`).
Pe branch-ul `main` se află doar acest index.

| Branch | Laborator | Folderul proiectului | Termen | Conținut |
|--------|-----------|----------------------|--------|----------|
| [`tema04`](../../tree/tema04) | #04 – transformări 2D, matricele model / view / projection, tastatura și mouse-ul | `EgcLab04/` | 07.11.2026, 20:00 | T1, T2, T3 (obligatorii), T4, T5 (facultative) |
| [`tema05`](../../tree/tema05) | #05 – mesh procedural, testul de adâncime, camera FPS și FOV | `EgcLab05/` | 14.11.2026, 12:00 | P1–P5, P6–P7 (facultative), tema: camera orbită (tasta V) + ambele bonusuri |

## Convenții respectate

- Fișierul `Program.cs` din fiecare proiect are pe prima linie un comentariu single-line cu adresa de e-mail de mai sus.
- Justificările cerute de teme sunt scrise ca comentarii în cod, lângă linia la care se referă, începând cu numărul temei (de exemplu `// T1: ...`, `// P2: ...`).
- Câte un commit pentru fiecare temă / problemă, cu mesajul începând cu numărul ei (de exemplu `T1: ...`).

## Compilare și rulare

Cu Visual Studio 2022: se deschide fișierul `.csproj` din folderul proiectului (File → Open → Project/Solution), apoi Build (Ctrl+Shift+B) și rulare (F5).
Pachetul OpenTK este declarat în proiect și se descarcă automat la primul build.

Din linia de comandă (.NET 8 SDK):

```bash
git clone -b tema04 https://github.com/tudorbaranai/Baranai_T_EGC_15.git
cd Baranai_T_EGC_15/EgcLab04
dotnet run
```

Pentru tema 05 se folosește `-b tema05` și folderul `EgcLab05`.
