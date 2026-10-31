using System.Collections.Generic;
using UnityEngine;

public sealed class NightGeometry
{
    readonly List<Vector3> v = new List<Vector3>();
    readonly List<Vector2> uv = new List<Vector2>();
    readonly List<int>[] t;
    public NightGeometry(int count=4) { t=new List<int>[count];for(int i=0;i<count;i++)t[i]=new List<int>(); }
    public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int mat)
    {
        int n=v.Count;v.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{new Vector2(a.z,a.y),new Vector2(b.z,b.y),new Vector2(c.z,c.y),new Vector2(d.z,d.y)});
        t[mat].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
    }
    public void Box(Vector3 p,Vector3 size,int mat,Quaternion? rotation=null)
    {
        var q=rotation??Quaternion.identity;Vector3 h=size*.5f;
        Vector3[] a={new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),
            new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z)};
        for(int i=0;i<a.Length;i++)a[i]=p+q*a[i];
        foreach(var f in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{0,1,5,4},new[]{3,7,6,2}})
            Quad(a[f[0]],a[f[1]],a[f[2]],a[f[3]],mat);
    }
    public void Beam(Vector3 a,Vector3 b,float width,float depth,int mat)
        =>Box((a+b)*.5f,new Vector3(width,depth,(b-a).magnitude),mat,Quaternion.LookRotation(b-a,Vector3.up));
    public void Cylinder(Vector3 a,Vector3 b,float radius,int mat,int sides=32)
    {
        var q=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        for(int j=0;j<sides;j++)
        {
            float x=j*Mathf.PI*2/sides,z=(j+1)*Mathf.PI*2/sides;
            Vector3 r=q*new Vector3(Mathf.Cos(x)*radius,0,Mathf.Sin(x)*radius),s=q*new Vector3(Mathf.Cos(z)*radius,0,Mathf.Sin(z)*radius);
            Quad(a+r,b+r,b+s,a+s,mat);Quad(a,a+s,a+r,a,mat);Quad(b,b+r,b+s,b,mat);
        }
    }
    public void Trapezoid(float backZ,float frontZ,float backHalf,float frontHalf,float y,float thickness,int mat)
    {
        Vector3 a=new Vector3(-backHalf,y,backZ),b=new Vector3(backHalf,y,backZ),c=new Vector3(frontHalf,y,frontZ),d=new Vector3(-frontHalf,y,frontZ),h=Vector3.down*thickness;
        Quad(a,d,c,b,mat);Quad(a+h,b+h,c+h,d+h,mat);
        Quad(a,b,b+h,a+h,mat);Quad(b,c,c+h,b+h,mat);Quad(c,d,d+h,c+h,mat);Quad(d,a,a+h,d+h,mat);
    }
    public void LatheY(Vector3 center,Vector2[] profile,int mat,int sides=64)
    {
        for(int i=0;i<profile.Length-1;i++)for(int j=0;j<sides;j++)
        {
            float a=j*Mathf.PI*2/sides,b=(j+1)*Mathf.PI*2/sides;
            Vector3 A=center+new Vector3(profile[i].y*Mathf.Cos(a),profile[i].x,profile[i].y*Mathf.Sin(a));
            Vector3 B=center+new Vector3(profile[i+1].y*Mathf.Cos(a),profile[i+1].x,profile[i+1].y*Mathf.Sin(a));
            Vector3 C=center+new Vector3(profile[i+1].y*Mathf.Cos(b),profile[i+1].x,profile[i+1].y*Mathf.Sin(b));
            Vector3 D=center+new Vector3(profile[i].y*Mathf.Cos(b),profile[i].x,profile[i].y*Mathf.Sin(b));
            int n=v.Count;Quad(A,B,C,D,mat);
            float h=profile[profile.Length-1].x-profile[0].x;
            uv[n]=new Vector2((float)j/sides,(profile[i].x-profile[0].x)/h);
            uv[n+1]=new Vector2((float)j/sides,(profile[i+1].x-profile[0].x)/h);
            uv[n+2]=new Vector2((float)(j+1)/sides,(profile[i+1].x-profile[0].x)/h);
            uv[n+3]=new Vector2((float)(j+1)/sides,(profile[i].x-profile[0].x)/h);
        }
    }
    public void Rotor(float r,float thick)
    {
        const int sides=144;
        // Two circular friction faces, beveled lips and a genuine ventilated gap.
        float[] radius={r*.30f,r*.34f,r*.97f,r};
        foreach(float side in new[]{-1f,1f})
        {
            float[] axle={side*thick*.38f,side*thick*.5f,side*thick*.5f,side*thick*.30f};
            for(int j=0;j<sides;j++)for(int k=0;k<radius.Length-1;k++)
            {
                float a=j*Mathf.PI*2/sides,b=(j+1)*Mathf.PI*2/sides;
                Quad(new Vector3(axle[k],Mathf.Cos(a)*radius[k],Mathf.Sin(a)*radius[k]),
                    new Vector3(axle[k],Mathf.Cos(b)*radius[k],Mathf.Sin(b)*radius[k]),
                    new Vector3(axle[k+1],Mathf.Cos(b)*radius[k+1],Mathf.Sin(b)*radius[k+1]),
                    new Vector3(axle[k+1],Mathf.Cos(a)*radius[k+1],Mathf.Sin(a)*radius[k+1]),0);
            }
            for(int j=0;j<24;j++)
            {
                float a=(j+.5f)*Mathf.PI*2/24;
                foreach(float band in new[]{.66f,.85f})
                {
                    var p=new Vector3(side*(thick*.5f+.00025f),Mathf.Cos(a)*r*band,Mathf.Sin(a)*r*band);
                    Cylinder(p,p+Vector3.right*side*.0005f,r*.024f,1,16);
                }
            }
        }
        for(int j=0;j<36;j++)
        {
            float a=j*Mathf.PI*2/36;
            var p=new Vector3(0,Mathf.Cos(a)*r*.66f,Mathf.Sin(a)*r*.66f);
            Box(p,new Vector3(thick*.56f,r*.60f,.006f),1,Quaternion.AngleAxis(a*Mathf.Rad2Deg,Vector3.right));
        }
        Cylinder(new Vector3(-.014f,0,0),new Vector3(.014f,0,0),r*.32f,1,72);
        Cylinder(new Vector3(-.020f,0,0),new Vector3(.020f,0,0),r*.18f,1,72);
        for(int j=0;j<6;j++)
        {
            float a=j*Mathf.PI/3;var p=new Vector3(0,Mathf.Cos(a)*r*.245f,Mathf.Sin(a)*r*.245f);
            Cylinder(p-Vector3.right*.021f,p+Vector3.right*.021f,.0075f,2,8);
        }
    }
    public void RoundedCaliper()
    {
        var outline=new List<Vector2>();
        for(int j=0;j<=18;j++){float a=j*Mathf.PI/18;outline.Add(new Vector2(Mathf.Cos(a)*.036f,.038f+Mathf.Sin(a)*.036f));}
        for(int j=0;j<=18;j++){float a=Mathf.PI+j*Mathf.PI/18;outline.Add(new Vector2(Mathf.Cos(a)*.036f,-.038f+Mathf.Sin(a)*.036f));}
        for(int j=0;j<outline.Count;j++)
        {
            var a=outline[j];var b=outline[(j+1)%outline.Count];
            Vector3 A=new Vector3(-.027f,a.x,a.y),B=new Vector3(-.027f,b.x,b.y),C=new Vector3(.027f,b.x*.88f,b.y*.94f),D=new Vector3(.027f,a.x*.88f,a.y*.94f);
            Quad(A,B,C,D,0);Quad(new Vector3(.027f,0,0),D,C,new Vector3(.027f,0,0),0);
            Quad(new Vector3(-.027f,0,0),B,A,new Vector3(-.027f,0,0),0);
        }
        foreach(float z in new[]{-.038f,0,.038f})Cylinder(new Vector3(.0275f,0,z),new Vector3(.030f,0,z),.019f,1,32);
        foreach(float z in new[]{-.055f,.055f})Cylinder(new Vector3(.03f,0,z),new Vector3(.033f,0,z),.005f,2,8);
    }
    public Mesh Mesh(string name)
    {
        var m=new Mesh {name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};m.SetVertices(v);m.SetUVs(0,uv);m.subMeshCount=t.Length;
        for(int i=0;i<t.Length;i++)m.SetTriangles(t[i],i);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;
    }
}
