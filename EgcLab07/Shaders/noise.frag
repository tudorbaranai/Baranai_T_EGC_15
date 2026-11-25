#version 330 core

// P6* (facultativ): zgomotul "valoare" (value noise) si marmura.

in vec3 vColor;
in vec2 vUV;

uniform bool uShowUV;
uniform float uTime;
uniform vec3 uColorA;        // culoarea de baza a marmurei
uniform vec3 uColorB;        // culoarea vinelor
uniform float uScale;        // marimea "granulelor" zgomotului
uniform float uTurbulence;   // cat de mult deformeaza zgomotul vinele

out vec4 FragColor;

// Un numar pseudo-aleator in [0, 1), acelasi de fiecare data pentru acelasi punct p.
float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Zgomotul: valori aleatoare in colturile unei grile, interpolate lin intre ele.
float valueNoise(vec2 p)
{
    // P6* (TODO 7.7 a): celula (i) si pozitia in celula (f).
    vec2 i = floor(p);
    vec2 f = fract(p);

    // P6*: cate o valoare aleatoare in fiecare colt al celulei; celulele vecine au colturile comune,
    // deci zgomotul este continuu (nu mai arata ca un mozaic).
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    // P6*: curba f * f * (3 - 2f) (EaseInOut din laboratorul 6) are panta 0 la marginea celulei,
    // deci trecerea de la o celula la alta nu are "cute". Interpolam pe x (jos si sus), apoi pe y.
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// "Fractal Brownian motion": 5 straturi (octave) de zgomot, fiecare de doua ori mai fin si mai slab.
float fbm(vec2 p)
{
    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 5; i++)
    {
        sum += amplitude * valueNoise(p);
        p *= 2.0;
        amplitude *= 0.5;
    }
    return sum;
}

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    vec2 p = vUV * uScale + vec2(uTime * 0.05, 0.0);

    // P6* (TODO 7.7 b): marmura = dungi sinusoidale (sin(p.x * 3)) deformate de zgomot. Cu uTurbulence = 0
    // dungile sunt drepte; cu cat creste, cu atat fbm le "indoaie" mai mult (vine incalcite).
    float veins = 0.5 + 0.5 * sin(p.x * 3.0 + uTurbulence * fbm(p));
    // P6*: puterea a treia pastreaza culoarea B doar aproape de minimul sinusului: vine subtiri si intense.
    float m = pow(1.0 - veins, 3.0);
    vec3 color = mix(uColorA, uColorB, m);
    FragColor = vec4(color * vColor, 1.0);
}
