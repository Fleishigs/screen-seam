using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// The renderer reads only the latest request. Slow captures never queue pointer work.
sealed class OverlayRenderer : IDisposable {
    readonly object gate=new object();
    readonly Thread thread;
    IntPtr surface;
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    public bool Showing {get{var h=Interlocked.CompareExchange(ref surface,IntPtr.Zero,IntPtr.Zero);return h!=IntPtr.Zero&&IsWindowVisible(h);}}
    public bool IsExposed(Point point){
        var h=Interlocked.CompareExchange(ref surface,IntPtr.Zero,IntPtr.Zero);
        return h!=IntPtr.Zero&&WindowFromPoint(point)==h;
    }
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
    bool raised;
    public void Lower(){if(!raised)return;raised=false;var h=Interlocked.CompareExchange(ref surface,IntPtr.Zero,IntPtr.Zero);if(h!=IntPtr.Zero)SetWindowPos(h,new IntPtr(-2),0,0,0,0,0x4013);}
    public void Raise(){raised=true;
        var h=Interlocked.CompareExchange(ref surface,IntPtr.Zero,IntPtr.Zero);
        if(h!=IntPtr.Zero)SetWindowPos(h,new IntPtr(-1),0,0,0,0,0x4013);
    }
    Rectangle source,dest; int mid,opacity; bool active,done;
    public OverlayRenderer(){
        thread=new Thread(Run);thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    public void Set(Rectangle s,Rectangle d,int middle,int alpha){
        lock(gate){source=s;dest=d;mid=middle;opacity=alpha;active=true;}
    }
    public void Hide(){Lower();lock(gate){active=false;}}
    void Run(){
        using(var window=new Strip())using(var timer=new System.Windows.Forms.Timer()){
            Interlocked.Exchange(ref surface,window.Handle);
            
            timer.Interval=10;
            timer.Tick+=delegate{
                timer.Stop();
                Rectangle s,d;int m,a;bool visible,stop;
                lock(gate){s=source;d=dest;m=mid;a=opacity;visible=active;stop=done;}
                if(stop){Application.ExitThread();return;}
                if(visible){
                    if(Math.Abs(window.Opacity-a/100.0)>.001)window.Opacity=a/100.0;
                    window.Render(s,d,m);
                }else window.ReleaseStrip();
                timer.Interval=visible?10:35;timer.Start();
            };
            timer.Start();Application.Run();Interlocked.Exchange(ref surface,IntPtr.Zero);
        }
    }
    public void Dispose(){lock(gate){done=true;}thread.Join();}
}
sealed class MirrorPointer : Form {
    [StructLayout(LayoutKind.Sequential)]struct Info{public int Size,Flags;public IntPtr Icon;public Point Position;}
    [DllImport("user32.dll")]static extern bool GetCursorInfo(ref Info ci);
    [DllImport("user32.dll")]static extern int GetSystemMetricsForDpi(int index,uint dpi);
    [DllImport("user32.dll")]static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int w,int h,uint step,IntPtr brush,uint flags);
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
    IntPtr icon;
    public MirrorPointer(){
        AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
        ShowInTaskbar=false;TopMost=true;BackColor=Color.Magenta;TransparencyKey=BackColor;
    }
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x20|0x80;return p;}}
    public void Track(Rectangle src,Rectangle dst,int mid,uint dpi){
        var ci=new Info{Size=Marshal.SizeOf(typeof(Info))};
        if(!GetCursorInfo(ref ci)||ci.Flags!=1||ci.Icon==IntPtr.Zero||!src.Contains(ci.Position)){Clear();return;}
        int w=GetSystemMetricsForDpi(13,dpi),h=GetSystemMetricsForDpi(14,dpi);
        Point hotspot;using(var c=new Cursor(ci.Icon)){hotspot=new Point(c.HotSpot.X*w/Math.Max(1,c.Size.Width),c.HotSpot.Y*h/Math.Max(1,c.Size.Height));}
        int x=dst.X+(int)Math.Round((ci.Position.X-src.X)*(dst.Width-1.0)/Math.Max(1,src.Width-1))-hotspot.X;
        int y=dst.Y+ScreenSeam.MapRow(ci.Position.Y-src.Y,src.Height,mid<0?-1:src.Height/2,dst.Height,mid)-hotspot.Y;
        bool repaint=icon!=ci.Icon;icon=ci.Icon;
        if(!Visible){Bounds=new Rectangle(x,y,w,h);Show();repaint=true;}
        SetWindowPos(Handle,new IntPtr(-1),x,y,w,h,0x10);
        if(repaint)Invalidate();
    }
    public void Clear(){if(Visible)Hide();}
    protected override void OnPaint(PaintEventArgs e){
        if(icon==IntPtr.Zero)return;var dc=e.Graphics.GetHdc();try{DrawIconEx(dc,0,0,icon,Width,Height,0,IntPtr.Zero,3);}finally{e.Graphics.ReleaseHdc(dc);}
    }
}



