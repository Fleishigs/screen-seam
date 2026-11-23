using System.Xml.Linq;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
[assembly: AssemblyProduct("Screen Seam")]
[assembly: AssemblyTitle("Screen Seam")]
[assembly: AssemblyDescription("A Windows utility for overlapping displays")]
[assembly: AssemblyVersion("2.4.1.0")]
[assembly: AssemblyFileVersion("2.4.1.0")]


sealed class Calibration {
    public decimal Left=25.5m, Right=84m, Top=75.5m;
    public bool Stitch=true;
    // Zero fills the calibrated rectangle. Positive values use the relative Windows DPIs.
    public int TextPercent=100, OverlayPercent=100;
    public Rectangle Overlay(Rectangle source,Rectangle laptop,uint upperDpi,uint lowerDpi){
        var measured=DisplayBand(source,laptop);
        if(TextPercent==0)return measured;
        double scale=lowerDpi/(double)Math.Max(1u,upperDpi)*TextPercent/100.0;
        scale=Math.Min(scale,Math.Min(measured.Width/(double)source.Width,measured.Height/(double)source.Height));
        int w=Math.Max(1,(int)Math.Round(source.Width*scale)),h=Math.Max(1,(int)Math.Round(source.Height*scale));
        return new Rectangle(measured.X+(measured.Width-w)/2,measured.Y,w,h);
    }
    internal static uint Dpi(Rectangle r){
        uint x,y;var center=new Point(r.Left+r.Width/2,r.Top+r.Height/2);
        return GetDpiForMonitor(MonitorFromPoint(center,2),0,out x,out y)==0&&x>0?x:96u;
    }
    public decimal LaptopLeft=0,LaptopRight=1,LaptopTop=-1,LaptopMiddle=-1,LaptopBottom=-1;
    public bool Measured {get{return LaptopTop>=0;}}
    static int Row(decimal fraction,int size){return (int)Math.Round(fraction*(size-1));}
    public Rectangle DisplayBand(Rectangle cut,Rectangle laptop){
        if(!Measured)return ScreenSeam.BandFor(cut,laptop);
        return Rectangle.FromLTRB(laptop.Left+Row(LaptopLeft,laptop.Width),laptop.Top+Row(LaptopTop,laptop.Height),
            laptop.Left+Row(LaptopRight,laptop.Width)+1,Math.Min(laptop.Bottom-1,laptop.Top+Row(LaptopBottom,laptop.Height)+1));
    }
    public int MiddleRow(int height){
        return Measured?Math.Max(1,Math.Min(height-2,Row((LaptopMiddle-LaptopTop)/(LaptopBottom-LaptopTop),height))):height/2;
    }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point pt,uint flags);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int kind,out uint x,out uint y);
    static string Display(Rectangle r){
        var p=new Point(r.Left+r.Width/2,r.Top+r.Height/2);uint dx,dy;
        return Screen.FromPoint(p).DeviceName+" "+r+(GetDpiForMonitor(MonitorFromPoint(p,2),0,out dx,out dy)==0?" "+dx+" DPI ("+Math.Round(dx*100.0/96)+"%)":"");
    }
    public static void CopyReport(Rectangle monitor,Rectangle laptop,Calibration c){
        var r=c.Cut(monitor);var b=c.DisplayBand(r,laptop);
        string report=string.Format("Screen Seam calibration (physical pixels)\r\nUpper display {0}\r\nLaptop display {1}\r\nCoordinates below are relative to each screen's top-left corner.\r\nUpper left/right: {2}, {3}; top/middle/bottom: {4}, {5}, {6}\r\nLaptop left/right: {7}, {8}; top/middle/bottom: {9}, {10}, {11}",Display(monitor),Display(laptop),r.Left-monitor.Left,r.Right-monitor.Left-1,r.Top-monitor.Top,r.Top-monitor.Top+r.Height/2,r.Bottom-monitor.Top-1,b.Left-laptop.Left,b.Right-laptop.Left-1,b.Top-laptop.Top,b.Top-laptop.Top+c.MiddleRow(b.Height),b.Bottom-laptop.Top-1);
        try{Clipboard.SetText(report);}catch(ExternalException){MessageBox.Show(report,"Screen Seam measurements");}
    }
    public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Screen Seam"); } }
    public Rectangle Cut(Rectangle r) {
        return Rectangle.FromLTRB(r.X+(int)Math.Round(r.Width*Left/100),r.Y+(int)Math.Round(r.Height*Top/100),r.X+(int)Math.Round(r.Width*Right/100),r.Bottom);
    }
    public bool Valid { get { return Left>=0 && Right<=100 && Right-Left>=10 && Top>=10 && Top<=99 && (!Measured||(LaptopLeft>=0&&LaptopRight<=1&&LaptopRight>LaptopLeft&&LaptopTop<LaptopMiddle&&LaptopMiddle<LaptopBottom&&LaptopBottom<1)); } }
    public XElement Xml() { return new XElement("seam",new XAttribute("left",Left),new XAttribute("right",Right),new XAttribute("top",Top),new XAttribute("stitch",Stitch),new XAttribute("lx",LaptopLeft),new XAttribute("rx",LaptopRight),new XAttribute("yt",LaptopTop),new XAttribute("ym",LaptopMiddle),new XAttribute("textPercent",TextPercent),new XAttribute("overlayPercent",OverlayPercent),new XAttribute("yb",LaptopBottom)); }
    public static Calibration Parse(XElement x) {
        var c=new Calibration {Left=(decimal)x.Attribute("left"),Right=(decimal)x.Attribute("right"),Top=(decimal)x.Attribute("top"),Stitch=(bool)x.Attribute("stitch")};
        c.LaptopLeft=(decimal?)x.Attribute("lx")??0;c.LaptopRight=(decimal?)x.Attribute("rx")??1;
        c.LaptopTop=(decimal?)x.Attribute("yt")??-1;c.LaptopMiddle=(decimal?)x.Attribute("ym")??-1;c.LaptopBottom=(decimal?)x.Attribute("yb")??-1;
        c.TextPercent=(int?)x.Attribute("textPercent")??100;c.OverlayPercent=(int?)x.Attribute("overlayPercent")??100;
        if(c.TextPercent!=0&&c.TextPercent!=100&&c.TextPercent!=110)c.TextPercent=100;
        c.OverlayPercent=Math.Max(30,Math.Min(100,c.OverlayPercent));
        if(!c.Valid) throw new InvalidDataException();
        return c;
    }
    public static Calibration Load() {
        try {return Parse(XElement.Load(Path.Combine(Folder,"settings.xml")));} catch {return new Calibration();}
    }
    public void Save() {
        Directory.CreateDirectory(Folder);
        string p=Path.Combine(Folder,"settings.xml"), t=p+".new";
        Xml().Save(t);
        if(File.Exists(p)) File.Replace(t,p,null); else File.Move(t,p);
    }
}
sealed class SettingsBox : Form {
    NumericUpDown left,right,top;
    CheckBox stitch; ComboBox textSize; NumericUpDown opacity;
    Panel diagram;
    Calibration baseline;
    Calibration Read() {var v=Calibration.Parse(baseline.Xml());v.Left=left.Value;v.Right=right.Value;v.Top=top.Value;v.Stitch=stitch.Checked;v.TextPercent=new[]{100,110,0}[textSize.SelectedIndex];v.OverlayPercent=(int)opacity.Value;return v;}
    NumericUpDown Field(string name,decimal value,int y) {
        Controls.Add(new Label {Text=name,Location=new Point(20,y+3),AutoSize=true});
        var n=new NumericUpDown {Location=new Point(246,y),Width=92,Minimum=0,Maximum=100,DecimalPlaces=1,Increment=.5m,Value=value};
        n.ValueChanged+=delegate {diagram.Invalidate();}; Controls.Add(n); return n;
    }
    public SettingsBox(Calibration c, Action<Calibration> preview, Action<Calibration> applied,Action wizard,Action copy) {
        baseline=c;
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Text="Screen Seam settings"; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
        AutoScaleDimensions=new SizeF(96,96); ClientSize=new Size(365,552); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
        diagram=new Panel {Location=new Point(20,12),Size=new Size(320,125)}; Controls.Add(diagram);
        left=Field("Laptop left edge (%)",c.Left,150); right=Field("Laptop right edge (%)",c.Right,186); top=Field("Laptop top edge (%)",c.Top,222);
        diagram.Paint+=delegate(object s,PaintEventArgs e) {
            Rectangle r=new Rectangle(8,4,300,110);
            e.Graphics.FillRectangle(Brushes.SteelBlue,r);
            var v=Read(); if(!v.Valid)return;
            var cut=v.Cut(r); e.Graphics.FillRectangle(Brushes.DarkSlateGray,cut);
            e.Graphics.DrawRectangle(Pens.White,cut);
        };
        Controls.Add(new Label {Text="Align the two vertical guides with the laptop sides and the horizontal guide with its top. Changes preview live.",Location=new Point(20,257),Size=new Size(325,53)});
        stitch=new CheckBox {Text="Show covered area on laptop",Checked=c.Stitch,Location=new Point(20,310),AutoSize=true}; Controls.Add(stitch);
        Controls.Add(new Label{Text="Overlay text size",Location=new Point(20,349),AutoSize=true});
        textSize=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(155,345),Width=184};
        textSize.Items.AddRange(new object[]{"Match laptop","10% larger","Fill calibrated area"});textSize.SelectedIndex=c.TextPercent==0?2:c.TextPercent==110?1:0;Controls.Add(textSize);
        Controls.Add(new Label{Text="Overlay opacity (%)",Location=new Point(20,391),AutoSize=true});
        opacity=new NumericUpDown{Minimum=30,Maximum=100,Increment=5,Value=c.OverlayPercent,Location=new Point(248,386),Width=91};Controls.Add(opacity);
        EventHandler changed=delegate {var draft=Read(); if(draft.Valid)preview(draft);};
        textSize.SelectedIndexChanged+=changed;opacity.ValueChanged+=changed;left.ValueChanged+=changed;right.ValueChanged+=changed;top.ValueChanged+=changed;stitch.CheckedChanged+=changed;
        var wizardButton=new Button{Text="Drag calibration lines...",Location=new Point(20,430),Size=new Size(232,35)};
        wizardButton.Click+=delegate{wizard();};Controls.Add(wizardButton);
        var copyButton=new Button{Text="Copy measurements",Location=new Point(20,482),Size=new Size(196,35)};
        copyButton.Click+=delegate{copy();};Controls.Add(copyButton);
        var save=new Button {Text="Apply",Location=new Point(244,482),Size=new Size(95,34)};
        save.Click+=delegate {var v=Read(); if(!v.Valid){MessageBox.Show("Leave at least 10% between the sides and set the top between 10% and 99%.");return;}
            try {v.Save();applied(v);Close();} catch(Exception ex){MessageBox.Show(ex.Message);} };
        Controls.Add(save); AcceptButton=save;
    }
}
sealed class Guides : Form {
    Rectangle cut;
    public Guides() {
        FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
        ShowInTaskbar=false;TopMost=true;BackColor=Color.Magenta;TransparencyKey=BackColor;AutoScaleMode=AutoScaleMode.None;
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get {var p=base.CreateParams;p.ExStyle|=0x08000000|0x20|0x80;return p;}}
    public void Place(Rectangle monitor,Rectangle overlap) {
        Bounds=monitor;cut=overlap;cut.Offset(-monitor.X,-monitor.Y);if(!Visible)Show();Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        using(var pen=new Pen(Color.DeepSkyBlue,3)){
            e.Graphics.DrawLine(pen,cut.Left,0,cut.Left,Height);e.Graphics.DrawLine(pen,cut.Right-1,0,cut.Right-1,Height);
            e.Graphics.DrawLine(pen,0,cut.Top,Width,cut.Top);
        }
    }
}
sealed class Strip : Form {
    int middle=-1; Bitmap frame; Rectangle source;
    public Strip() {AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual; ShowInTaskbar=false; TopMost=false; DoubleBuffered=true; BackColor=Color.Black;}
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get {var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool StretchBlt(IntPtr dest,int x,int y,int w,int h,IntPtr src,int sx,int sy,int sw,int sh,uint op);
    [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr dc,int mode);
    [DllImport("gdi32.dll")] static extern bool SetBrushOrgEx(IntPtr dc,int x,int y,IntPtr old);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h,uint command);
    [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr parent);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder name,int count);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int WindowStyle(IntPtr h,int index);
    void PlaceAboveDesktop(){
        var name=new System.Text.StringBuilder(128);
        for(var h=GetTopWindow(IntPtr.Zero);h!=IntPtr.Zero;h=GetWindow(h,2)){
            if(!IsWindowVisible(h))continue;
            GetClassName(h,name,name.Capacity);
            if(name.ToString()!="Progman"&&name.ToString()!="WorkerW")continue;
            var previous=GetWindow(h,3);
            if(previous!=Handle){if(previous!=IntPtr.Zero && (WindowStyle(previous,-20)&8)!=0)previous=new IntPtr(-2);SetWindowPos(Handle,previous,0,0,0,0,0x13);}
            return;
        }
        SetWindowPos(Handle,new IntPtr(1),0,0,0,0,0x13);
    }
    protected override void WndProc(ref Message msg){
        if(msg.Msg==0x21){msg.Result=new IntPtr(3);return;}
        base.WndProc(ref msg);
    }
    public void ReleaseStrip(){if(Visible)Hide();}
    public void Render(Rectangle src,Rectangle dest,int middleRow=-1){
        middle=middleRow;source=src;
        if(Bounds!=dest)Bounds=dest;
        if(!Visible){Show();PlaceAboveDesktop();}
        Invalidate();Update();
    }
    protected override void OnPaintBackground(PaintEventArgs e){}
    protected override void OnPaint(PaintEventArgs e){
        if(source.IsEmpty)return;
        IntPtr src=GetDC(IntPtr.Zero);if(src==IntPtr.Zero)return;
        IntPtr dc=e.Graphics.GetHdc();
        bool ok=false;
        try{
            SetStretchBltMode(dc,4);SetBrushOrgEx(dc,0,0,IntPtr.Zero);
            const uint copy=0x00CC0020;
            if(middle>0&&middle<Height-1&&source.Height>=3){
                int half=source.Height/2;
                ok=StretchBlt(dc,0,0,Width,middle,src,source.X,source.Y,source.Width,half,copy)
                    &&StretchBlt(dc,0,middle,Width,Height-middle,src,source.X,source.Y+half,source.Width,source.Height-half,copy);
            }else ok=StretchBlt(dc,0,0,Width,Height,src,source.X,source.Y,source.Width,source.Height,copy);
        }finally{e.Graphics.ReleaseHdc(dc);ReleaseDC(IntPtr.Zero,src);}
        if(!ok){
            if(frame==null||frame.Size!=source.Size){if(frame!=null)frame.Dispose();frame=new Bitmap(source.Width,source.Height);}
            try{using(var g=Graphics.FromImage(frame))g.CopyFromScreen(source.Location,Point.Empty,source.Size);}
            catch(System.ComponentModel.Win32Exception){return;}
            if(middle>0&&middle<Height-1&&frame.Height>=3){
                int h=frame.Height/2;
                e.Graphics.DrawImage(frame,new Rectangle(0,0,Width,middle),new Rectangle(0,0,frame.Width,h),GraphicsUnit.Pixel);
                e.Graphics.DrawImage(frame,new Rectangle(0,middle,Width,Height-middle),new Rectangle(0,h,frame.Width,frame.Height-h),GraphicsUnit.Pixel);
            }else e.Graphics.DrawImage(frame,ClientRectangle,new Rectangle(Point.Empty,frame.Size),GraphicsUnit.Pixel);
        }
    }
    protected override void Dispose(bool disposing){if(disposing){ReleaseStrip();if(frame!=null)frame.Dispose();}base.Dispose(disposing);}
}
class ScreenSeam : Form {
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
    readonly NotifyIcon tray=new NotifyIcon();
    readonly OverlayRenderer renderer=new OverlayRenderer();
    readonly MirrorPointer pointerImage=new MirrorPointer();
    uint laptopDpi;
    readonly Func<Point> readPointer;
    readonly Action<Point> movePointer;
    readonly Guides guides=new Guides();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    SettingsBox settings;
    Calibration config=Calibration.Load(), draft;
    Calibration Active {get{return draft??config;}}
    Rectangle m,l,cut,band,desiredBand,work;
    Point last; bool ready,paused,calibrating; int tick;
    bool RefreshScreens(){
        var s=Screen.AllScreens;if(s.Length!=2)return false;
        var a=s[0];var b=s[1];if(a.Bounds.Top>b.Bounds.Top){a=s[1];b=s[0];}
        if(a.Bounds.Bottom!=b.Bounds.Top||Math.Min(a.Bounds.Right,b.Bounds.Right)<=Math.Max(a.Bounds.Left,b.Bounds.Left))return false;
        m=a.Bounds;l=b.Bounds;cut=Active.Cut(m);work=a.WorkingArea;
        laptopDpi=Calibration.Dpi(l);desiredBand=Active.Overlay(cut,l,Calibration.Dpi(m),laptopDpi);return true;
    }
    // A physical column on the upper panel occupies Width/cut.Width columns on the lower panel.
    public static Rectangle BandFor(Rectangle c,Rectangle laptop){
        double scale=Math.Min(laptop.Width/(double)c.Width,(laptop.Height-2)/(double)c.Height);
        int w=Math.Max(1,(int)Math.Round(c.Width*scale)),h=Math.Max(1,(int)Math.Round(c.Height*scale));
        return new Rectangle(laptop.X+(laptop.Width-w)/2,laptop.Y,w,h);
    }
    static int Map(int n,int a,int w,int b,int v){double z=Math.Max(0,Math.Min(1,(n-a)/(double)Math.Max(1,w-1)));return b+(int)Math.Round(z*(v-1));}
    internal static int MapRow(int y,int from,int pivot,int to,int destPivot){
        if(from<3||to<3||pivot<=0||pivot>=from-1)return Map(y,0,from,0,to);
        return y<pivot?Map(y,0,pivot+1,0,destPivot+1):Map(y,pivot,from-pivot,destPivot,to-destPivot);
    }
    public static Point Route(Point old,Point p,Rectangle m,Rectangle l,Rectangle c,Rectangle b,bool stitch,int middle=-1) {
        if(stitch) {
            if(m.Contains(old) && l.Contains(p)) {
                if(old.X<c.Left||old.X>=c.Right)return new Point(old.X,m.Bottom-1);
                int x=Math.Max(c.Left,Math.Min(c.Right-1,p.X));
                int y=Math.Min(l.Bottom-1,b.Bottom+Math.Max(0,p.Y-l.Top));
                return new Point(Map(x,c.Left,c.Width,b.Left,b.Width),y);
            }
            // Windows clamps at a physical edge when the native display rectangles do not meet here.
            if(c.Contains(p)&&p.Y==m.Bottom-1&&old.Y<p.Y&&(p.X<l.Left||p.X>=l.Right))
                return new Point(Map(p.X,c.Left,c.Width,b.Left,b.Width),b.Bottom);
            if(b.Contains(p))return new Point(Map(p.X,b.Left,b.Width,c.Left,c.Width),MapRow(p.Y-b.Top,b.Height,middle,c.Height,c.Height/2)+c.Top);
            if(l.Contains(old)&&m.Contains(p)){
                if(old.X<b.Left||old.X>=b.Right)return p;
                return new Point(Map(old.X,b.Left,b.Width,c.Left,c.Width),c.Top);
            }
            return p;
        }
        if(m.Contains(old)&&c.Contains(p)){
            if(old.Y<c.Top)return new Point(Map(p.X,c.Left,c.Width,l.Left,l.Width),l.Top+2);
            return new Point(old.X<c.Left?l.Left+2:l.Right-3,l.Top+p.Y-c.Top);
        }
        if(m.Contains(old)&&l.Contains(p))return new Point(old.X,m.Bottom-1);
        if(l.Contains(old)){
            if(m.Contains(p)||(p.Y==l.Top&&old.Y>l.Top))return new Point(Map(old.X,l.Left,l.Width,c.Left,c.Width),c.Top-2);
            int y=c.Top+p.Y-l.Top;
            if(y>=c.Top&&y<c.Bottom){
                if(p.X==l.Left&&old.X>l.Left)return new Point(c.Left-2,y);
                if(p.X==l.Right-1&&old.X<l.Right-1)return new Point(c.Right+1,y);
            }
        }
        return p;
    }
    [StructLayout(LayoutKind.Sequential)] struct InputState {
        public uint Size,Flags;public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;public int Left,Top,Right,Bottom;
    }
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint id,ref InputState state);
    bool CapturingInput(){
        var state=new InputState{Size=(uint)Marshal.SizeOf(typeof(InputState))};
        return GetGUIThreadInfo(0,ref state)&&(state.Capture!=IntPtr.Zero||state.MoveSize!=IntPtr.Zero||(state.Flags&0x1e)!=0);
    }
    Point VisibleRoute(Point p,int middle){
        if(!Active.Stitch)return Route(last,p,m,l,cut,band,false);
        bool captured=CapturingInput();
        if(band.Contains(p)&&(!renderer.IsExposed(p)||(captured&&l.Contains(last))))return p;
        if(l.Contains(p)&&l.Contains(last)&&captured)return p;
        if(cut.Contains(p)&&renderer.Showing&&!captured){
            var projected=new Point(Map(p.X,cut.Left,cut.Width,band.Left,band.Width),
                band.Top+MapRow(p.Y-cut.Top,cut.Height,middle<0?-1:cut.Height/2,band.Height,middle));
            if(!renderer.IsExposed(projected))return projected;
        }
        if(last==p)return p;
        return Route(last,p,m,l,cut,band,true,middle);
    }
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    bool wasPressed;
    internal void MirrorPress(Point p,int middle){
        Point visible=p;
        if(cut.Contains(p))visible=new Point(Map(p.X,cut.Left,cut.Width,band.Left,band.Width),
            band.Top+MapRow(p.Y-cut.Top,cut.Height,middle<0?-1:cut.Height/2,band.Height,middle));
        if(band.Contains(visible)&&renderer.IsExposed(visible))renderer.Raise();else renderer.Lower();
    }
    void OnTick(object sender,EventArgs e) {
        if(calibrating)return;
        if(++tick%40==0)ready=RefreshScreens();
        if(!ready||paused){guides.Hide();HideOverlay();last=readPointer();return;}
        band=desiredBand;
        int middle=Active.TextPercent==0&&Active.Measured?Active.MiddleRow(band.Height):-1;
        var p=readPointer();
        short buttonState=GetAsyncKeyState(1);bool pressed=(buttonState&0x8000)!=0;
        if(Active.Stitch&&(pressed||(buttonState&1)!=0)&&!wasPressed)MirrorPress(p,middle);wasPressed=pressed;
        var dest=VisibleRoute(p,middle);if(dest!=p){movePointer(dest);p=dest;}
        last=p;
        if(Active.Stitch){renderer.Set(cut,band,middle,Active.OverlayPercent);pointerImage.Track(cut,band,middle,laptopDpi);}else HideOverlay();
    }
    void HideOverlay(){renderer.Hide();pointerImage.Clear();}
    void Fit(){
        var p=readPointer();if(!ready||paused)return;
        if(Active.Stitch&&l.Contains(p)) {FitLaptop();return;}
        if(!m.Contains(p)||cut.Contains(p))return;
        var zone=Rectangle.Intersect(work,p.Y<cut.Top?Rectangle.FromLTRB(m.Left,m.Top,m.Right,cut.Top):p.X<cut.Left?Rectangle.FromLTRB(m.Left,cut.Top,cut.Left,m.Bottom):Rectangle.FromLTRB(cut.Right,cut.Top,m.Right,m.Bottom));
        var w=GetForegroundWindow();if(w==IntPtr.Zero||zone.Width<1||zone.Height<1)return;
        if(IsZoomed(w))ShowWindow(w,9);SetWindowPos(w,IntPtr.Zero,zone.X,zone.Y,zone.Width,zone.Height,0x14);
    }
    void FitLaptop(){
        var w=GetForegroundWindow();if(w==IntPtr.Zero)return;
        Rectangle area=Screen.FromPoint(l.Location).WorkingArea;

        if(area.Height<1)return;
        if(IsZoomed(w))ShowWindow(w,9);
        SetWindowPos(w,IntPtr.Zero,area.X,area.Y,area.Width,area.Height,0x14);
    }
    void Toggle(){if(calibrating)return;paused=!paused;if(paused)HideOverlay();last=readPointer();tray.Text=paused?"Screen Seam (paused)":"Screen Seam";}
    void StartWizard(){
        if(!ready){MessageBox.Show("Stack the two displays in Windows Display settings before calibrating.");return;}
        calibrating=true;HideOverlay();guides.Hide();settings.Hide();
        try{
            using(var wizard=new ClickCalibration(m,l,Active)){
                var result=wizard.Run();
                if(result!=null){result.TextPercent=Active.TextPercent;result.OverlayPercent=Active.OverlayPercent;result.Save();config=result;}
            }
        }catch(Exception ex){MessageBox.Show(ex.Message,"Screen Seam calibration");}
        finally{settings.Close();calibrating=false;last=readPointer();ready=RefreshScreens();}
    }
    void Configure(){
        if(calibrating)return;
        if(settings!=null&&!settings.IsDisposed){settings.Activate();return;}
        draft=config;
        settings=new SettingsBox(config,delegate(Calibration value){
            draft=value;ready=RefreshScreens();if(ready&&!paused)guides.Place(m,cut);
        },delegate(Calibration value){config=value;},StartWizard,delegate{if(ready)Calibration.CopyReport(m,l,Active);});
        settings.FormClosed+=delegate{draft=null;guides.Hide();ready=RefreshScreens();};
        if(ready){
            settings.StartPosition=FormStartPosition.Manual;settings.Location=new Point(m.Left+20,m.Top+20);
            if(!paused)guides.Place(m,cut);
        }
        settings.Show();settings.Activate();
    }
    protected override void WndProc(ref Message msg){
        if(msg.Msg==0x312){if(msg.WParam.ToInt32()==1)Fit();else Toggle();}base.WndProc(ref msg);
    }
    internal ScreenSeam(Func<Point> reader=null, Action<Point> mover=null){
        readPointer=reader??delegate{return Cursor.Position;};
        movePointer=mover??delegate(Point target){Cursor.Position=target;};
        bool diagnostic=reader!=null;
        if(diagnostic)config=new Calibration();ready=RefreshScreens();last=readPointer();
        var menu=new ContextMenuStrip();menu.Items.Add("Adjust screen edges…",null,delegate{Configure();});
        menu.Items.Add("Pause / resume",null,delegate{Toggle();});
        menu.Items.Add("Exit",null,delegate{Application.ExitThread();});
        tray.Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);tray.Text="Screen Seam";tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Configure();};tray.Visible=true;
        RegisterHotKey(Handle,1,0x4003,0x79);RegisterHotKey(Handle,2,0x4003,0x7A);
        timer.Interval=15;timer.Tick+=OnTick;timer.Start();if(!diagnostic)Configure();
    }
    protected override void Dispose(bool disposing){
        if(disposing){timer.Dispose();UnregisterHotKey(Handle,1);UnregisterHotKey(Handle,2);if(settings!=null)settings.Dispose();renderer.Dispose();pointerImage.Dispose();guides.Dispose();tray.Visible=false;tray.Dispose();}
        base.Dispose(disposing);
    }
    [STAThread] static int Main(string[] args){
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        if(args.Length>0&&args[0]=="--test")return SeamTests.Run(args);
        bool first;using(var mutex=new Mutex(true,"Local\\SceptreScreenSeam_v1",out first)){
            if(!first){MessageBox.Show("Screen Seam is already running. Open its tray icon to adjust it. If the old portable version is running, exit it first.");return 0;}
            Application.EnableVisualStyles();using(var app=new ScreenSeam())Application.Run();
        }return 0;
    }
}





