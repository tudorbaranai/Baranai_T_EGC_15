#version 330 core

// TEMA 7, varianta B: vertex shaderul scutului. Ca procedural.vert, dar calculeaza si cat de "pe margine"
// este punctul, privit din camera (vRim): 0 pe mijlocul sferei, aproape de 1 pe contur. Pentru o sfera
// centrata in origine, normala este chiar pozitia varfului; o ducem in view space, unde camera priveste
// de-a lungul axei -Z, deci |n.z| mic inseamna o suprafata vazuta din lateral (marginea).

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor;
layout (location = 2) in vec2 aUV;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vColor;
out vec2 vUV;
out float vRim;

void main()
{
    vColor = aColor;
    vUV = aUV;
    vec3 normalView = normalize(mat3(uView * uModel) * aPosition);
    vRim = 1.0 - abs(normalView.z);
    gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
}
