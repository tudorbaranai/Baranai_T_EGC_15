#version 330 core

// TEMA 7, varianta B: bara de viata, desenata in intregime de acest shader pe un singur dreptunghi (Quad).
//  - forma: un dreptunghi cu colturi rotunjite, dat prin distanta (SDF), cu rama inchisa la culoare;
//  - interiorul este impartit in uSegments segmente (spatii subtiri intre ele);
//  - culoarea depinde de valoare: verde -> galben -> rosu, cu smoothstep intre ele;
//  - sub 25% (uCritical) bara pulseaza;
//  - "urma": portiunea pierduta (intre uValue si uTrail) ramane vizibila, deschisa la culoare;
//  - la lovitura bara clipeste (uFlash, scade exponential in C#).
// Toate marginile sunt netede: smoothstep pe latimea unui pixel (fwidth). In afara formei alfa = 0.
// Comentariile sunt fara diacritice: unele drivere accepta doar ASCII in GLSL.

in vec3 vColor;
in vec2 vUV;

uniform bool uShowUV;       // tasta U
uniform float uTime;        // timpul de joc (pulsul se opreste in pauza)

uniform float uValue;       // viata afisata, 0..1 (dintr-un Tween: scade / creste lin)
uniform float uTrail;       // capatul urmei (viata de dinainte de lovitura), >= uValue cat timp se vede
uniform float uFlash;       // 1 la lovitura, apoi scade spre 0
uniform bool uCritical;     // viata sub 25%: bara pulseaza
uniform int uSegments;      // numarul de segmente (reglabil cu sagetile)
uniform float uCorner;      // raza colturilor, in unitati de lume (reglabila)
uniform vec2 uBarSize;      // latimea si inaltimea dreptunghiului (pentru colturi rotunde, nu ovale)
uniform vec3 uColorFull;    // culoarea la viata plina
uniform vec3 uColorMid;     // ... la jumatate
uniform vec3 uColorLow;     // ... aproape de 0

out vec4 FragColor;

// Distanta (cu semn) pana la un dreptunghi cu colturi rotunjite, centrat in origine (Inigo Quilez):
// negativa in interior, 0 pe margine, pozitiva in afara. b = jumatatile laturilor, r = raza colturilor.
float sdRoundBox(vec2 p, vec2 b, float r)
{
    vec2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

// 1 in interiorul formei (d < 0), 0 in afara, cu o trecere neteda de un pixel.
float fill(float d)
{
    float aa = fwidth(d);
    return 1.0 - smoothstep(-aa, aa, d);
}

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    // Coordonatele in unitati de lume, cu originea in centrul dreptunghiului: asa colturile sunt
    // cercuri adevarate, chiar daca bara este de 7 ori mai lata decat inalta.
    vec2 p = (vUV - 0.5) * uBarSize;
    vec2 hs = 0.5 * uBarSize;
    float border = 0.05;

    float outer = fill(sdRoundBox(p, hs, uCorner));                                       // toata bara
    float inner = fill(sdRoundBox(p, hs - border, max(uCorner - border, 0.001)));        // fara rama

    // Pozitia pe lungimea interiorului, 0 (stanga) .. 1 (dreapta).
    float x = clamp((p.x + hs.x - border) / (uBarSize.x - 2.0 * border), 0.0, 1.0);
    float px = fwidth(x);

    // Partea plina (x < uValue) si urma (uValue <= x < uTrail), cu margini de un pixel.
    float filled = 1.0 - smoothstep(uValue - px, uValue + px, x);
    float trail = (1.0 - smoothstep(uTrail - px, uTrail + px, x)) * (1.0 - filled);

    // Culoarea dupa valoare: rosu -> galben intre 15% si 40%, galben -> verde intre 45% si 70%.
    vec3 color = mix(uColorLow, uColorMid, smoothstep(0.15, 0.40, uValue));
    color = mix(color, uColorFull, smoothstep(0.45, 0.70, uValue));

    // Sub 25%: pulsul (fara if: mix cu 0 sau 1).
    float pulse = 0.5 + 0.5 * sin(uTime * 9.0);
    color *= mix(1.0, 0.65 + 0.55 * pulse, float(uCritical));

    // Un usor relief: partea de sus a barei este mai deschisa.
    color *= 0.85 + 0.3 * smoothstep(-hs.y, hs.y, p.y);

    // Segmentele: o linie subtire la fiecare 1 / uSegments din lungime. db = distanta (in segmente) pana la
    // cea mai apropiata granita k / n; linia are grosimea 0.08 segmente, cu margini netede (fwidth).
    float n = float(max(uSegments, 1));
    float db = abs(fract(x * n + 0.5) - 0.5);
    float sw = fwidth(x * n);
    float gap = 1.0 - smoothstep(0.04, 0.04 + sw, db);
    // Fara linii la capete (acolo este rama); pragurile sunt la mijlocul primului / ultimului segment,
    // unde gap este oricum 0, deci step nu produce nicio treapta vizibila.
    gap *= step(0.5 / n, x) * step(x, 1.0 - 0.5 / n);

    vec3 empty = vec3(0.10, 0.11, 0.14);
    vec3 trailColor = mix(color, vec3(1.0), 0.65);
    vec3 inside = mix(empty, trailColor, trail);
    inside = mix(inside, color, filled);
    inside = mix(inside, empty * 0.6, gap * 0.8);

    vec3 frame = vec3(0.04, 0.045, 0.06);
    vec3 result = mix(frame, inside, inner);

    // Clipirea la lovitura: toata bara spre alb.
    result = mix(result, vec3(1.0), 0.75 * clamp(uFlash, 0.0, 1.0));

    FragColor = vec4(result * vColor, outer);
}
