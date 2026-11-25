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
| [`tema04`](https://github.com/tudorbaranai/Baranai_T_EGC_15/tree/tema04) | #04 – transformări 2D, matricele model / view / projection, tastatura și mouse-ul | `EgcLab04/` | 07.11.2026, 20:00 | T1, T2, T3 (obligatorii), T4, T5 (facultative) |
| [`tema05`](https://github.com/tudorbaranai/Baranai_T_EGC_15/tree/tema05) | #05 – mesh procedural, testul de adâncime, camera FPS și FOV | `EgcLab05/` | 14.11.2026, 12:00 | P1–P5, P6–P7 (facultative), tema: camera orbită (tasta V) + ambele bonusuri |
| [`tema06`](https://github.com/tudorbaranai/Baranai_T_EGC_15/tree/tema06) | #06 – animații bazate pe timp (ceas, easing, tween, cadre-cheie), input prin acțiuni | `EgcLab06/` | 21.11.2026, 12:00 | P1–P4, P5 (facultativ), tema: varianta B – evită obstacolele (Tab) + bonus |
| [`tema07`](https://github.com/tudorbaranai/Baranai_T_EGC_15/tree/tema07) | #07 – shadere GLSL editabile, valori uniforme, culori procedurale | `EgcLab07/` | 01.12.2026, 20:00 | P1–P5, P6 (facultativ), tema: varianta B – bara de viață și scutul (Tab) + bonus |

## Convenții respectate

- Fișierul `Program.cs` din fiecare proiect are pe prima linie un comentariu single-line cu adresa de e-mail de mai sus.
- De la laboratorul 06, fișierul scenei temei (folderul `Teme/`) are pe primul rând varianta aleasă.
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

Pentru celelalte teme se folosește branch-ul și folderul corespunzător (de exemplu `-b tema07` și `EgcLab07`).
