using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
class SeamTests {
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int cmd);
 static int checks;
 static void Assert(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Pump(){for(int i=0;i<10;i++){Application.DoEvents();Thread.Sleep(15);}}
 static Color Pixel(Point p){using(var img=new Bitmap(1,1)){using(var g=Graphics.FromImage(img))g.CopyFromScreen(p,Point.Empty,img.Size);return img.GetPixel(0,0);}}
 [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
 [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
 [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dest,int x,int y,int w,int h,IntPtr src,int sx,int sy,uint flags);
 static Color CompositePixel(Point pos){
    using(var bm=new Bitmap(1,1)){using(var g=Graphics.FromImage(bm)){
        var dc=g.GetHdc();var screen=GetDC(IntPtr.Zero);try{BitBlt(dc,0,0,1,1,screen,pos.X,pos.Y,0x40CC0020);}finally{ReleaseDC(IntPtr.Zero,screen);g.ReleaseHdc(dc);}
    }return bm.GetPixel(0,0);}
 }
 public static int Run(string[] args){
 string report="";
 try{
 foreach(int width in new[]{1366,1920,2560,3840})foreach(int offset in new[]{-300,0,384})foreach(int top in new[]{12,50,75,99}){
 var u=new Rectangle(offset,-1080,width,1080);var d=new Rectangle(0,0,1920,1080);
 var cut=new Calibration{Top=top}.Cut(u);var band=ScreenSeam.BandFor(cut,d);
 Assert(d.Contains(band)&&band.Bottom<d.Bottom,"bounds");Assert(Math.Abs(band.Height-band.Width*cut.Height/(double)cut.Width)<2,"distorted aspect");
 for(int i=0;i<101;i++){
 int x=cut.X+(cut.Width-1)*i/100;int lx=band.Left+(band.Width-1)*i/100;
 var back=ScreenSeam.Route(new Point(lx,band.Bottom+2),new Point(lx,band.Bottom-1),u,d,cut,band,true);
 Assert(back.Y==cut.Bottom-1&&Math.Abs(back.X-x)<4,"reverse seam");
 if(x>=d.Left&&x<d.Right){
 var forward=ScreenSeam.Route(new Point(x,-1),new Point(x,4),u,d,cut,band,true);
 Assert(forward.Y==Math.Min(d.Bottom-1,band.Bottom+4)&&Math.Abs(forward.X-lx)<5,"forward seam");
 Assert(ScreenSeam.Route(new Point(x,-3),new Point(x,-1),u,d,cut,band,true).Y==-1,"inaccessible taskbar");
 }}
 }
 report+="Geometry checks passed: "+checks+Environment.NewLine;
 var screens=Screen.AllScreens.OrderBy(v=>v.Bounds.Top).ToArray();
 var m=screens[0].Bounds;var l=screens[1].Bounds;var c=new Calibration().Cut(m);var b=ScreenSeam.BandFor(c,l);
 using(var f=new Form())using(var strip=new Strip())using(var button=new Button()){
 f.FormBorderStyle=FormBorderStyle.None;f.StartPosition=FormStartPosition.Manual;f.AutoScaleMode=AutoScaleMode.None;f.Bounds=c;f.TopMost=true;f.BackColor=Color.SeaGreen;
 button.Bounds=new Rectangle(30,30,120,36);button.Text="Target";int clicks=0;button.Click+=delegate{clicks++;};f.Controls.Add(button);f.Show();Pump();
 strip.Render(c,b);strip.Refresh();Pump();
 report+="Monitor "+m+" DPI "+GetDpiForWindow(f.Handle)+" laptop "+l+" DPI "+GetDpiForWindow(strip.Handle)+Environment.NewLine;
 Assert(f.Bounds==c&&strip.Bounds==b,"unexpected bounds "+f.Bounds+" "+strip.Bounds);
 var sample=new Point(b.X+b.Width/2,b.Y+b.Height/2);Assert(Pixel(sample).ToArgb()==Color.SeaGreen.ToArgb(),"screen capture sample "+Pixel(sample));
 var mapped=ScreenSeam.Route(new Point(b.X,b.Bottom+1),new Point(b.X+65*b.Width/c.Width,b.Y+45*b.Height/c.Height),m,l,c,b,true);
 Assert(WindowFromPoint(mapped)==button.Handle,"click hit test");
 SendMessage(WindowFromPoint(mapped),0xF5,IntPtr.Zero,IntPtr.Zero);Assert(clicks==1,"button activation");
 f.BackColor=Color.DarkOrange;f.Refresh();Pump();strip.Render(c,b);strip.Refresh();Pump();Assert(Pixel(sample).ToArgb()==Color.DarkOrange.ToArgb(),"live update");
 strip.ReleaseStrip();Assert(!strip.Visible,"pause");
 b=new Calibration().Overlay(c,l,Calibration.Dpi(m),Calibration.Dpi(l));
 var pointer=new Point(b.X+70,b.Bottom+20);
 Rectangle priorWork=Screen.FromPoint(l.Location).WorkingArea;
 using(var app=new ScreenSeam(delegate{return pointer;},delegate(Point v){pointer=v;})){
 Pump();
 var to=new Point(b.X+65*b.Width/c.Width,b.Y+45*b.Height/c.Height);
 pointer=to;Pump();
 Assert(WindowFromPoint(pointer)==button.Handle,"timer hit wrong control "+pointer);
 SendMessage(WindowFromPoint(pointer),0xF5,IntPtr.Zero,IntPtr.Zero);
 Assert(clicks==2,"click on timer-mapped target");
 using(var front=new Form())using(var localButton=new Button()){
    front.StartPosition=FormStartPosition.Manual;front.FormBorderStyle=FormBorderStyle.Sizable;
    front.Location=new Point(to.X-45,to.Y-36);front.ClientSize=new Size(220,140);
    localButton.Dock=DockStyle.Fill;int localClicks=0;localButton.Click+=delegate{localClicks++;};front.Controls.Add(localButton);
    front.Show();ShowWindow(front.Handle,5);SetWindowPos(front.Handle,IntPtr.Zero,0,0,0,0,0x13);Pump();
    var localPoint=localButton.PointToScreen(new Point(50,40));
    Assert(b.Contains(localPoint)&&WindowFromPoint(localPoint)==localButton.Handle,"laptop test button must be visible");
    var exposed=new Point(b.Right-30,localPoint.Y);pointer=exposed;Pump();
    Assert(c.Contains(pointer),"exposed part of partially covered mirror lost input");
    pointer=localPoint;Pump();Assert(pointer==localPoint,"covered laptop control routed behind");
    SendMessage(WindowFromPoint(pointer),0xF5,IntPtr.Zero,IntPtr.Zero);Assert(localClicks==1&&clicks==2,"wrong receiver for covered button");
    var title=new Point(front.Left+65,front.Top+13);pointer=title;Pump();Assert(pointer==title&&WindowFromPoint(title)==front.Handle,"covered title bar unreachable");
    pointer=new Point(c.X+(localPoint.X-b.X)*c.Width/b.Width,c.Y+(localPoint.Y-b.Y)*c.Height/b.Height);Pump();
    Assert(WindowFromPoint(pointer)==localButton.Handle,"approach from upper screen failed");
    var uncovered=new Point(b.Right-25,localPoint.Y);
    app.MirrorPress(localPoint,-1);Pump();Assert(WindowFromPoint(localPoint)==localButton.Handle,"laptop click raised mirror");
    app.MirrorPress(uncovered,-1);Pump();Assert(Control.FromHandle(WindowFromPoint(localPoint)) is Strip,"exposed click did not raise whole mirror");
    front.Hide();Pump();pointer=new Point(to.X,b.Bottom+4);Pump();pointer=to;Pump();
    Assert(WindowFromPoint(pointer)==button.Handle,"exposed mirror did not resume");
 }
 report+="Foreground laptop control, title bar, upper-screen approach, and exposed-mirror routing passed."+Environment.NewLine;
 pointer=new Point(c.X+c.Width/2,-3);Pump();pointer=new Point(c.X+c.Width/2,2);Pump();
 Assert(Screen.FromPoint(l.Location).WorkingArea==priorWork,"laptop desktop resized");
 Assert(pointer.Y>=b.Bottom,"downward timer handoff "+pointer);
 SendMessage(app.Handle,0x312,new IntPtr(2),IntPtr.Zero);Pump();
 pointer=to;Pump();Assert(pointer==to,"paused mouse moved");
 SendMessage(app.Handle,0x312,new IntPtr(2),IntPtr.Zero);Pump();
 }

 }
 var nativeSize=new Calibration();var native=nativeSize.Overlay(c,l,96,120);
 Assert(Math.Abs(native.Width-c.Width*1.25)<=1&&Math.Abs(native.Height-c.Height*1.25)<=1,"laptop DPI sizing");
 nativeSize.TextPercent=110;var larger=nativeSize.Overlay(c,l,96,120);
 Assert(Math.Abs(larger.Width-c.Width*1.375)<=1,"110 percent DPI sizing");
 nativeSize.TextPercent=0;Assert(nativeSize.Overlay(c,l,96,120)==nativeSize.DisplayBand(c,l),"full calibration preserved");
 report+="Laptop-size, 10% larger, and calibrated-size modes passed."+Environment.NewLine;
 using(var src=new Form())using(var under=new Form())using(var renderer=new OverlayRenderer()){
    src.FormBorderStyle=under.FormBorderStyle=FormBorderStyle.None;
    src.StartPosition=under.StartPosition=FormStartPosition.Manual;
    src.Bounds=c;src.BackColor=Color.Red;under.Bounds=new Rectangle(l.Left+5,l.Top+5,l.Width-10,l.Height-75);under.BackColor=Color.Blue;
    src.TopMost=true;under.TopMost=false;src.Show();under.Show();ShowWindow(under.Handle,5);src.Refresh();under.Refresh();Pump();
    var unchanged=under.Bounds;var before=Screen.FromPoint(l.Location).WorkingArea;
    var probe=new Point(native.X+native.Width/2,native.Y+native.Height/2);
    renderer.Set(c,native,-1,100);Pump();SetWindowPos(under.Handle,IntPtr.Zero,0,0,0,0,0x13);renderer.Raise();Pump();
    Assert(Pixel(probe).R>230,"background renderer did not show source");
    renderer.Set(c,native,-1,50);Pump();Pump();var mix=CompositePixel(probe);for(int retry=0;retry<10 && (mix.B<90||mix.R>165);retry++){Pump();mix=CompositePixel(probe);}
    Assert(mix.R>90&&mix.R<165&&mix.B>90&&mix.B<165,"opacity blend "+mix);
    Assert(under.Bounds==unchanged&&Screen.FromPoint(l.Location).WorkingArea==before,"overlay moved laptop window/work area");
    renderer.Hide();Pump();Assert(CompositePixel(probe).B>230,"underlying laptop view not restored");
 }
 report+="Background rendering, 50% blending, unchanged laptop window and desktop work area, and reveal passed."+Environment.NewLine;
 using(var wizard=new ClickCalibration(m,l,new Calibration()))using(var driver=new System.Windows.Forms.Timer()){
 int phase=0;int[] marks={580,1580,810,11,1880,8,220,459};Exception fail=null;
 driver.Interval=120;driver.Tick+=delegate{
 try{
 var panels=Application.OpenForms.OfType<MarkPanel>().ToArray();
 Assert(panels.Length==2,"wizard did not show both displays");
 var up=panels.First(pn=>pn.Upper);var panel=phase<3?up:panels.First(pn=>!pn.Upper);
 Assert(panel.Enabled,"calibration screen disabled");
 if(phase<8){
 int n=marks[phase];bool vertical=phase==0||phase==1||phase==3||phase==4;
 int x=vertical?n:700,y=vertical?350:n;
 SendMessage(panel.Handle,513,new IntPtr(1),new IntPtr((350<<16)|700));
 SendMessage(panel.Handle,512,new IntPtr(1),new IntPtr((y<<16)|x));
 SendMessage(panel.Handle,514,IntPtr.Zero,new IntPtr((y<<16)|x));
 up.Controls.OfType<Button>().First(v=>v.Text=="Next").PerformClick();phase++;
 }else{driver.Stop();up.Controls.OfType<Button>().First(v=>v.Text=="Save").PerformClick();}
 }catch(Exception e){fail=e;driver.Stop();var up=Application.OpenForms.OfType<MarkPanel>().FirstOrDefault(v=>v.Upper);if(up!=null)up.Command(Keys.Escape);}
 };
 driver.Start();var result=wizard.Run();if(fail!=null)throw fail;Assert(result!=null,"wizard save failed");
 result=Calibration.Parse(result.Xml());var rect=result.Cut(m);var band=result.DisplayBand(rect,l);
 Assert(rect.Left-m.Left==580&&rect.Right-m.Left-1==1580&&rect.Top-m.Top==810,"upper drag measurements");
 Assert(band.X-l.X==11&&band.Right-l.X-1==1880&&band.Top-l.Top==8&&band.Bottom-l.Top-1==459,"laptop drag measurements");
 Assert(band.Y-l.Y+result.MiddleRow(band.Height)==220,"middle guide round trip");
 var atMiddle=new Point(band.X+44,band.Top+result.MiddleRow(band.Height));
 Assert(ScreenSeam.Route(new Point(atMiddle.X,band.Bottom+3),atMiddle,m,l,rect,band,true,result.MiddleRow(band.Height)).Y==rect.Top+rect.Height/2,"middle anchor pointer");
 report+="Eight draggable guides, both wizard windows, Save, settings round trip, and exact midpoint mapping passed."+Environment.NewLine;
 }
 report+="Native capture, displayed pixel, updated pixel, mapped control click and pause passed.";return 0;
 }catch(Exception e){report+=e;return 1;}finally{if(args.Length>1)File.WriteAllText(args[1],report);}
 }
}










