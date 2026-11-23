using System;
using System.Drawing;
using System.Windows.Forms;

sealed class MarkPanel : Form {
    internal readonly bool Upper;
    internal Action<MarkPanel,Point> Mark; bool dragging;
    internal Action<Keys> Command;
    internal Action<MarkPanel,Graphics> PaintGuides;
    public MarkPanel(Rectangle bounds,bool upper){
        Upper=upper;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
        AutoScaleMode=AutoScaleMode.None;Bounds=bounds;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;
        BackColor=Color.FromArgb(24,36,46);ForeColor=Color.White;Cursor=Cursors.Cross;KeyPreview=true;
    }
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(PaintGuides!=null)PaintGuides(this,e.Graphics);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){dragging=true;Capture=true;Mark(this,e.Location);}}
    protected override void OnMouseMove(MouseEventArgs e){if(dragging)Mark(this,e.Location);}
    protected override void OnMouseUp(MouseEventArgs e){if(dragging){Mark(this,e.Location);dragging=false;Capture=false;}}
    protected override bool ProcessCmdKey(ref Message msg,Keys key){
        if(Command!=null&&(key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||key==Keys.Enter||key==Keys.Escape||key==Keys.Back)){Command(key);return true;}
        return base.ProcessCmdKey(ref msg,key);
    }
    protected override void OnKeyDown(KeyEventArgs e){if(Command!=null)Command(e.KeyCode);e.Handled=true;e.SuppressKeyPress=true;}
}
sealed class ClickCalibration : IDisposable {
    readonly MarkPanel upper,lower; readonly Rectangle m,l; readonly Calibration original;
    readonly int[] marks=new int[8]; int step; bool closing;
    readonly Button back=new Button(), next=new Button(), copy=new Button();
    readonly string[] names={"Upper monitor: laptop LEFT edge","Upper monitor: laptop RIGHT edge","Upper monitor: laptop TOP edge",
        "Laptop: LEFT side","Laptop: RIGHT side","Laptop: TOP line","Laptop: MIDDLE line","Laptop: BOTTOM line","Review alignment"};
    public Calibration Result;
    public ClickCalibration(Rectangle monitor,Rectangle laptop,Calibration current){
        m=monitor;l=laptop;original=current;upper=new MarkPanel(m,true);lower=new MarkPanel(l,false);
        var c=current.Cut(m);var band=current.DisplayBand(c,l);
        marks[0]=c.Left-m.X;marks[1]=c.Right-m.X-1;marks[2]=c.Top-m.Y;
        marks[3]=band.X-l.X;marks[4]=band.Right-l.X-1;marks[5]=band.Y-l.Y;
        marks[6]=marks[5]+current.MiddleRow(band.Height);marks[7]=band.Bottom-l.Y-1;
        foreach(var panel in new[]{upper,lower}){
            panel.Mark=Drag;panel.Command=Key;panel.PaintGuides=Draw;
            panel.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!closing){e.Cancel=true;((Form)sender).BeginInvoke(new Action(delegate{Finish(false);}));}};
        }
        AddButton(back,"Back",24,delegate{Advance(-1);});
        AddButton(next,"Next",140,delegate{Advance(1);});
        AddButton(copy,"Copy measurements",256,delegate{Calibration.CopyReport(m,l,Value());});
        upper.Shown+=delegate{lower.Show(upper);upper.Activate();}; RefreshPanels();
    }
    void AddButton(Button button,string text,int x,Action action){
        button.Text=text;button.SetBounds(x,102,text.StartsWith("Copy")?170:104,36);button.TabStop=false;
        button.Click+=delegate{action();};upper.Controls.Add(button);
    }
    public Calibration Run(){upper.ShowDialog();return Result;}
    void RefreshPanels(){back.Enabled=step>0;next.Text=step==8?"Save":"Next";upper.Invalidate();lower.Invalidate();}
    Calibration Value(){return Measurements(m,l,marks,original.Stitch);}
    void Finish(bool save){if(closing)return;if(save)Result=Value();closing=true;lower.Hide();upper.DialogResult=save?DialogResult.OK:DialogResult.Cancel;}
    internal static Calibration Measurements(Rectangle m,Rectangle l,int[] v,bool stitch){
        decimal dx=l.Width-1,dy=l.Height-1;
        return new Calibration{Left=100m*v[0]/m.Width,Right=100m*(v[1]+1)/m.Width,Top=100m*v[2]/m.Height,
            LaptopLeft=v[3]/dx,LaptopRight=v[4]/dx,LaptopTop=v[5]/dy,LaptopMiddle=v[6]/dy,LaptopBottom=v[7]/dy,Stitch=stitch};
    }
    void Advance(int delta){if(step==8&&delta>0)Finish(true);else{step=Math.Max(0,Math.Min(8,step+delta));RefreshPanels();}}
    void Key(Keys k){
        if(k==Keys.Escape)Finish(false);else if(k==Keys.Enter)Advance(1);else if(k==Keys.Back)Advance(-1);
        else if(step<8&&(k==Keys.Left||k==Keys.Up||k==Keys.Down||k==Keys.Right))
            Set(marks[step]+(k==Keys.Up||k==Keys.Left?-1:1));
    }
    bool Vertical(int i){return i==0||i==1||i==3||i==4;}
    void Drag(MarkPanel panel,Point p){if(step<8&&panel.Upper==(step<3))Set(Vertical(step)?p.X:p.Y);}
    void Set(int n){
        int[] lo={0,marks[0]+(m.Width+9)/10-1,(m.Height+9)/10,0,marks[3]+9,0,marks[5]+2,marks[6]+2};
        int[] hi={marks[1]-(m.Width+9)/10+1,m.Width-1,m.Height*99/100,marks[4]-9,l.Width-1,marks[6]-2,marks[7]-2,l.Height-3};
        marks[step]=Math.Max(lo[step],Math.Min(hi[step],n));RefreshPanels();
    }
    void Draw(MarkPanel panel,Graphics g){
        int width=panel.ClientSize.Width,height=panel.ClientSize.Height;
        int[] ids=panel.Upper?new[]{0,1,2,6,7}:new[]{3,4,5,6,7};
        using(var label=new Font("Segoe UI",11,FontStyle.Bold)){
            foreach(int id in ids){
                int n=marks[id];
                if(panel.Upper&&id>=6)n=id==6?marks[2]+(height-marks[2])/2:height-1;
                bool active=step==id||(panel.Upper&&((step==3&&id==0)||(step==4&&id==1)||(step==5&&id==2)));
                using(var pen=new Pen(active?Color.Gold:Color.DeepSkyBlue,active?3:1)){
                    if(Vertical(id))g.DrawLine(pen,n,0,n,height);else g.DrawLine(pen,0,n,width,n);
                    string name=Vertical(id)?(id==0||id==3?"Left":"Right"):id==6?"Middle":id==7?"Bottom":"Top";
                    g.DrawString(name+" "+n+" px",label,active?Brushes.Gold:Brushes.White,
                        Vertical(id)?Math.Max(3,Math.Min(width-120,n+4)):4,Vertical(id)?height/3:Math.Max(2,n-24));
                }
            }
        }
        if(panel.Upper){using(var font=new Font("Segoe UI",15))using(var small=new Font("Segoe UI",11)){
            g.FillRectangle(Brushes.Black,15,12,Math.Min(width-30,780),85);
            g.DrawString((step<8?(step+1)+" / 8    ":"")+names[step],font,Brushes.White,24,18);
            g.DrawString(step==8?"Compare all five guides on both screens. Save or copy the measurements.":"Drag anywhere on the named screen to move the gold line. Arrow keys nudge 1 pixel.",small,Brushes.Gold,24,57);
        }}
    }
    public void Dispose(){closing=true;upper.Dispose();lower.Dispose();}
}
