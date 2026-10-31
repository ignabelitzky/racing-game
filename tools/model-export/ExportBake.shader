Shader "Hidden/Tumbler/ExportBake"
{
    Properties { _BaseMap("Base",2D)="white"{} _NormalMap("Normal",2D)="bump"{} _MaskMap("Mask",2D)="white"{} }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _BaseMap,_NormalMap,_MaskMap;
            float4 _Tint,_ST;
            float _Mode,_UVMode,_WorldScale,_HasNormal,_HasMask,_NormalStrength,_Metallic,_Smoothness;
            float4 _Remap; // metallic min/max, smoothness min/max
            struct Input {float4 vertex:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float2 bakeUV:TEXCOORD1;};
            struct Varying {float4 clip:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;float4 t:TEXCOORD2;float2 uv:TEXCOORD3;float2 bakeUV:TEXCOORD4;};
            Varying vert(Input v)
            {
                Varying o;o.clip=float4(v.bakeUV*2-1,.5,1);
                #if UNITY_UV_STARTS_AT_TOP
                o.clip.y=-o.clip.y;
                #endif
                o.p=v.vertex.xyz;o.n=v.normal;o.t=v.tangent;o.uv=v.uv*_ST.xy+_ST.zw;o.bakeUV=v.bakeUV;return o;
            }
            float3 weights(float3 n){float3 w=max(pow(abs(n)-.2,3),0);return w/max(dot(w,1),.00001);}
            float4 sampleMap(sampler2D map,Varying i,float3 w)
            {
                if(_UVMode<4.5)return tex2D(map,i.uv);
                float3 p=i.p*_WorldScale;
                return tex2D(map,p.zy*_ST.xy+_ST.zw)*w.x+tex2D(map,p.xz*_ST.xy+_ST.zw)*w.y+tex2D(map,p.xy*_ST.xy+_ST.zw)*w.z;
            }
            float3 normalSample(float2 uv){float3 n=UnpackNormal(tex2D(_NormalMap,uv));n.xy*=_NormalStrength;n.z=sqrt(saturate(1-dot(n.xy,n.xy)));return n;}
            float4 frag(Varying i):SV_Target
            {
                float3 n=normalize(i.n),w=weights(n);
                if(_Mode<.5)return sampleMap(_BaseMap,i,w)*_Tint;
                float4 mask=sampleMap(_MaskMap,i,w);
                if(_Mode<1.5){float m=_HasMask>.5?lerp(_Remap.x,_Remap.y,mask.r):_Metallic;return float4(m,m,m,1);}
                if(_Mode<2.5){float r=1-(_HasMask>.5?lerp(_Remap.z,_Remap.w,mask.a):_Smoothness);return float4(r,r,r,1);}
                float3 perturbed=n;
                if(_HasNormal>.5)
                {
                    if(_UVMode<4.5)
                    {
                        float3 t=normalize(i.t.xyz),b=normalize(cross(n,t))*i.t.w;
                        float3 q=normalSample(i.uv);perturbed=normalize(t*q.x+b*q.y+n*q.z);
                    }
                    else
                    {
                        float3 p=i.p*_WorldScale;
                        float3 x=normalSample(p.zy*_ST.xy+_ST.zw),y=normalSample(p.xz*_ST.xy+_ST.zw),z=normalSample(p.xy*_ST.xy+_ST.zw);
                        // Whiteout blend retains the geometric normal when the map is neutral.
                        x=float3(abs(x.z)*n.x,x.y+n.y,x.x+n.z);
                        y=float3(y.x+n.x,abs(y.z)*n.y,y.y+n.z);
                        z=float3(z.x+n.x,z.y+n.y,abs(z.z)*n.z);
                        perturbed=normalize(x*w.x+y*w.y+z*w.z);
                    }
                }
                float3 dx=ddx(i.p),dy=ddy(i.p);float2 ux=ddx(i.bakeUV),uy=ddy(i.bakeUV);
                float determinant=ux.x*uy.y-ux.y*uy.x;
                float3 t=(dx*uy.y-dy*ux.y)*(determinant<0?-1:1);
                float3 b=(dy*ux.x-dx*uy.x)*(determinant<0?-1:1);
                t=normalize(t-n*dot(n,t));float handed=dot(cross(n,t),b)<0?-1:1;b=normalize(cross(n,t))*handed;
                float3 q=normalize(float3(dot(perturbed,t),dot(perturbed,b),dot(perturbed,n)));
                return float4(q*.5+.5,1);
            }
            ENDHLSL
        }
    }
}
