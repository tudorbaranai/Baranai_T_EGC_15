#version 330 core

// TEMA 7, varianta B: scutul, o sfera transparenta in jurul personajului.
//  - benzi de energie orizontale care urca in timp (uTime), usor ondulate;
//  - marginea (vRim) mai luminoasa, ca un camp de forta; conturul se stinge lin (fara trepte);
//  - uStrength (Tween din C#, tasta Space) porneste / opreste scutul lin;
//  - uHit = clipirea la o lovitura primita cu scutul pornit.

in vec3 vColor;
in vec2 vUV;
in float vRim;

uniform bool uShowUV;
uniform float uTime;

uniform float uStrength;      // 0 = oprit, 1 = pornit (trecere lina)
uniform float uHit;           // 1 la lovitura, apoi scade spre 0
uniform vec3 uShieldColor;    // culoarea scutului
uniform float uBandCount;     // cate benzi de la polul sud la polul nord (reglabil)
uniform float uBandSpeed;     // viteza benzilor (reglabila)

out vec4 FragColor;

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    // Benzile: o sinusoida pe v, deplasata in timp (urca), deformata putin dupa u (unde).
    float wobble = 0.08 * sin(vUV.x * 6.2831853 * 3.0 + uTime * 1.7);
    float arg = (vUV.y + wobble) * uBandCount - uTime * uBandSpeed;
    float band = 0.5 + 0.5 * sin(6.2831853 * arg);
    // Benzi subtiri cu margini netede: smoothstep pe latimea unui pixel in jurul pragului 0.8.
    float aa = fwidth(band);
    band = smoothstep(0.8 - aa, 0.8 + aa, band);

    // Marginea luminoasa; chiar pe contur alfa scade lin la 0, ca silueta sferei sa nu aiba trepte.
    float rim = pow(clamp(vRim, 0.0, 1.0), 2.5);
    float edgeFade = 1.0 - smoothstep(0.9, 1.0, vRim);

    float alpha = (0.06 + 0.35 * band + 0.55 * rim + 0.5 * uHit) * uStrength * edgeFade;
    vec3 color = uShieldColor * (0.6 + 0.6 * band + 0.8 * rim);
    color = mix(color, vec3(1.0), clamp(uHit, 0.0, 1.0) * 0.7);

    FragColor = vec4(color * mix(vec3(1.0), vColor, 0.25), clamp(alpha, 0.0, 1.0));
}
