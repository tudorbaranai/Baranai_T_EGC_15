#version 330 core

// P5: paleta cosinus (Inigo Quilez): o singura formula, patru vectori, o infinitate de palete.

in vec3 vColor;
in vec2 vUV;

uniform bool uShowUV;
uniform float uTime;
uniform vec3 uA;          // luminozitatea medie a fiecarui canal (r, g, b)
uniform vec3 uB;          // amplitudinea (cat de mult variaza canalul)
uniform vec3 uC;          // frecventa (de cate ori se repeta canalul cand t creste cu 1)
uniform vec3 uD;          // faza (decalajul canalelor intre ele)
uniform float uRepeat;    // de cate ori se repeta paleta de la polul sud la polul nord
uniform float uSpeed;     // viteza de "curgere" a culorilor

out vec4 FragColor;

vec3 palette(float t)
{
    // P5 (TODO 7.6 a): culoare(t) = a + b * cos(2 * pi * (c * t + d)). uC * t + uD este un vec3, deci
    // cos calculeaza trei cosinusuri deodata, cate unul pentru fiecare canal (r, g, b).
    // P5 (intrebare): ca un canal sa se repete de doua ori mai des, se dubleaza componenta lui din c
    // (frecventa), de exemplu c = (1, 2, 1) pentru verde; a, b si d nu schimba perioada.
    return uA + uB * cos(6.2831853 * (uC * t + uD));
}

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    // P5 (TODO 7.6 b): t creste de la polul sud (v = 0) la polul nord (v = 1), de uRepeat ori, si curge
    // in timp. De ce benzile COBOARA: o culoare fixa corespunde unui t constant; cand uTime creste,
    // vUV.y trebuie sa scada ca suma sa ramana aceeasi, deci fiecare culoare se muta spre polul sud.
    float t = vUV.y * uRepeat + uTime * uSpeed;
    vec3 color = palette(t);
    FragColor = vec4(color * vColor, 1.0);
}
