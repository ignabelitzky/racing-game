using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent dashboard symbols with no font-glyph dependency.</summary>
public sealed class VehicleStatusIcon : MaskableGraphic
{
    public enum Symbol { LightsOff, LowBeam, HighBeam, Handbrake, Bat }
    [SerializeField] private Symbol symbol;
    public void SetSymbol(Symbol value) { if (symbol == value) return; symbol = value; SetVerticesDirty(); }
    private VertexHelper mesh;
    private Vector2 P(Vector2 v) { var r = rectTransform.rect; return new Vector2(r.xMin + v.x * r.width, r.yMin + v.y * r.height); }
    private void Line(Vector2 a, Vector2 b, float width = .055f)
    {
        a=P(a);b=P(b);Vector2 n=(b-a).normalized; n=new Vector2(-n.y,n.x)*rectTransform.rect.width*width*.5f;
        int i=mesh.currentVertCount;
        foreach(var p in new[]{a+n,b+n,b-n,a-n})mesh.AddVert(p,color,Vector2.zero);
        mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
    }
    private void Arc(Vector2 c,float r,float from,float to,int count=24)
    {
        for(int i=0;i<count;i++)
        {float a=Mathf.Lerp(from,to,(float)i/count),b=Mathf.Lerp(from,to,(float)(i+1)/count);Line(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r);}
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();mesh=vh;
        if(symbol==Symbol.Handbrake)
        {
            Arc(new Vector2(.5f,.5f),.31f,0,Mathf.PI*2);
            Arc(new Vector2(.5f,.5f),.44f,.72f*Mathf.PI,1.28f*Mathf.PI,12);
            Arc(new Vector2(.5f,.5f),.44f,-.28f*Mathf.PI,.28f*Mathf.PI,12);
            Vector2[] p={new Vector2(.43f,.3f),new Vector2(.43f,.71f),new Vector2(.61f,.71f),new Vector2(.64f,.58f),new Vector2(.43f,.54f)};
            for(int i=0;i<p.Length-1;i++)Line(p[i],p[i+1]);return;
        }
        if(symbol==Symbol.Bat)
        {
            Vector2[] p={new Vector2(.04f,.72f),new Vector2(.16f,.43f),new Vector2(.30f,.49f),new Vector2(.38f,.28f),
                new Vector2(.46f,.37f),new Vector2(.50f,.14f),new Vector2(.54f,.37f),new Vector2(.62f,.28f),new Vector2(.70f,.49f),
                new Vector2(.84f,.43f),new Vector2(.96f,.72f),new Vector2(.78f,.63f),new Vector2(.67f,.69f),new Vector2(.59f,.60f),
                new Vector2(.56f,.83f),new Vector2(.50f,.74f),new Vector2(.44f,.83f),new Vector2(.41f,.60f),new Vector2(.33f,.69f),new Vector2(.22f,.63f)};
            vh.AddVert(P(new Vector2(.5f,.55f)),color,Vector2.zero);
            foreach(var v in p)vh.AddVert(P(v),color,Vector2.zero);
            for(int i=0;i<p.Length;i++)vh.AddTriangle(0,i+1,(i+1)%p.Length+1);return;
        }
        Arc(new Vector2(.53f,.5f),.31f,-Mathf.PI*.5f,Mathf.PI*.5f,16);
        Line(new Vector2(.53f,.19f),new Vector2(.53f,.81f));
        if(symbol==Symbol.LightsOff)Line(new Vector2(.10f,.14f),new Vector2(.89f,.88f));
        else for(int i=0;i<3;i++)
        {
            float y=.31f+i*.20f;
            Line(new Vector2(.10f, y - (symbol == Symbol.LowBeam ? .16f : 0f)),new Vector2(.43f,y));
        }
    }
}
