#version 330 core

// P4: un disc cu margine neteda si unde concentrice animate.

in vec3 vColor;
in vec2 vUV;

uniform bool uShowUV;
uniform float uTime;           // timpul de joc, in secunde (se opreste in pauza)
uniform vec2 uCenter;          // centrul discului, in coordonate UV
uniform float uRadius;         // raza discului, in coordonate UV
uniform float uSoftness;       // latimea marginii netede
uniform float uFrequency;      // cate unde pe unitatea de distanta
uniform float uSpeed;          // viteza undelor
uniform vec3 uColorInside;     // culoarea undelor
uniform vec3 uColorOutside;    // culoarea din afara discului

out vec4 FragColor;

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    // Distanta fragmentului fata de centrul discului (0 in centru, ~0.5 la marginea panoului).
    float d = distance(vUV, uCenter);

    // P4 (TODO 7.5 a): discul. Varianta "dura", step(d, uRadius), are margine in trepte (aliasing):
    // fiecare pixel este complet in disc sau complet in afara. smoothstep face trecerea pe latimea
    // 2 * uSoftness: 1 in interior, 0 in afara, o curba lina (EaseInOut) intre ele.
    float inside = 1.0 - smoothstep(uRadius - uSoftness, uRadius + uSoftness, d);

    // P4 (TODO 7.5 b): undele. O creasta este acolo unde argumentul sinusului este constant; cand uTime
    // creste, d trebuie sa creasca si el, deci crestele se departeaza de centru. 0.5 + 0.5 * sin aduce
    // rezultatul din [-1, 1] in [0, 1], ca sa poata fi folosit in mix. uTime = timpul de joc: P le opreste.
    // P4 (intrebare 1): cu + in loc de -, crestele se misca spre centru (undele "vin" spre centru).
    // P4 (intrebare 2): cu abs(vUV.x - uCenter.x) in loc de distance, "distanta" depinde doar de x: in loc
    // de cercuri apar dungi verticale care pleaca spre stanga si spre dreapta, iar discul devine o banda
    // verticala de latime 2 * uRadius.
    float wave = 0.5 + 0.5 * sin(6.2831853 * (d * uFrequency - uTime * uSpeed * 0.25));

    vec3 ringColor = mix(uColorInside * 0.35, uColorInside, wave);
    vec3 color = mix(uColorOutside, ringColor, inside);
    FragColor = vec4(color * vColor, 1.0);
}
