using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Net;

namespace GoogleMapDownLoader
{
    public partial class MainForm : Form
    {
        ArrayList _waittodownload = new ArrayList();  //待下载图片集合
        string _path=""; //保存路径
        int _thread = 0;  //下载线程数目
        int _downloadnum = 0;  //已下载图片张数
        int _zoom=0;  //缩放级别

        int _totalwidth = 0;  //地图合并之后的宽度
        int _totalheight = 0;  //地图合并之后的高度

        DateTime _startTime = DateTime.Now;  //开始下载时间
        public MainForm()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 浏览保存目录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                if (fb.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtPath.Text = fb.SelectedPath;
                }
            }
        }
        /// <summary>
        /// 开始下载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            double flng=0;  //左上经度
            double flat=0;  //左上纬度
            double slng=0;  //右下经度
            double slat=0;  //右下纬度
            int zomm=0;  //缩放级别
            int thread=4;  //下载线程数目
            try
            {
                _path = txtPath.Text;
                if (_path == "")
                {
                    throw new Exception();
                }
                zomm = (int)numZoom.Value;
                thread = (int)numThread.Value;
                flng = double.Parse(txtfirstlng.Text);
                flat = double.Parse(txtfirstlat.Text);
                slng = double.Parse(txtsecondlng.Text);
                slat = double.Parse(txtsecondlat.Text);
            }
            catch
            {
                MessageBox.Show("参数设置异常！");
            }
            Point p = LatLongToPixel(flat, flng, zomm); //将第一个点经纬度转换成平面2D坐标
            Point p2 = LatLongToPixel(slat, slng, zomm);  //将第二个点经纬度转换成平面2D坐标
            int startX = p.X / 256;  //起始列
            int endX = p2.X / 256;   //结束列
            if (endX == Math.Pow(2, zomm))  //结束列超出范围
            {
                endX--;
            }
            int startY = p.Y / 256;  //起始行
            int endY = p2.Y / 256;   //结束行
            if (endY == Math.Pow(2, zomm))  //结束行超出范围
            {
                endY--;
            }
            //以上由startX endX startY endY 围成的区域 即为待下载区域  该区域由许多256*256大小方块组成

            _totalwidth = (endX - startX + 1) * 256;  //合并图的宽度
            _totalheight = (endY - startY + 1) * 256;  //合并图的高度

            int serverId = 0;
            int threadId = 0;
            _waittodownload.Clear();
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    RectInfo ri = new RectInfo();
                    ri.serverId = serverId;  //分别从不同的服务器下载
                    ri.threadId = threadId;  //分别由不同的线程下载
                    ri.url = BuildURL(serverId);
                    ri.x = x;
                    ri.y = y;
                    ri.z = zomm;
                    ri.bComplete = false;
                    _waittodownload.Add(ri);  //将每个小方块放入待下载集合
                    serverId = (serverId + 1) % 4;   //从4个不同的服务器上下载图片
                    threadId = (threadId + 1) % thread;  //由thread个不同线程下载图片
                }
            }

            if (MessageBox.Show("共有" + _waittodownload.Count + "张图片需要下载，确定下载吗？", "提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.OK)
            {
                _thread = thread;
                _zoom = zomm;
                _downloadnum = 0;
                groupBox1.Enabled = false;
                button2.Enabled = false;
                linkLabel1.Enabled = false;
                rchOuput.Clear();
                groupBox2.Text = "输出(" + _waittodownload.Count + "张)";
                _startTime = DateTime.Now;
                for (int i = 1; i <= thread; ++i)
                {
                    Thread t = new Thread(new ParameterizedThreadStart(DownloadThreadProc));
                    t.Start(i);  //开启下载线程
                }
            }
        }
        /// <summary>
        /// 浏览地图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_downloadnum == _waittodownload.Count && _downloadnum != 0)  //全部下载完毕
            {
                Bitmap b = new Bitmap(_totalwidth, _totalheight);
                Graphics g = Graphics.FromImage(b);
                int startx = ((RectInfo)(_waittodownload[0])).x;
                int starty = ((RectInfo)(_waittodownload[0])).y;
                foreach (RectInfo rf in _waittodownload)
                {
                    g.DrawImage(rf.Bitmap, new Point((rf.x - startx) * 256, (rf.y - starty) * 256));
                }
                g.Dispose();
                b.Save(_path + "\\" + _zoom + "_total.jpg");
                b.Dispose();
                System.Diagnostics.Process.Start(_path + "\\" + _zoom + "_total.jpg");
            }
        }
        #region help methods
        /// <summary>
        /// 根据服务器id创建地图下载url的前缀
        /// </summary>
        /// <param name="serverId"></param>
        /// <returns></returns>
        private string BuildURL(int serverId)
        {
            if (radioButton7.Checked)  //国外服务器  类似 http://mts0.googleapis.com/vt?lyrs=m&x=0&y=0&z=0
            {
                string url = "";
                url = "http://mts";
                url += serverId.ToString();
                url += ".googleapis.com/vt?lyrs=";

                if (radioButton1.Checked)  //路线图
                {
                    url += "m";
                }
                if (radioButton2.Checked)  //卫星图
                {
                    url += "s";
                }
                if (radioButton3.Checked)  //卫星图 带标签
                {
                    url += "y";
                }
                if (radioButton4.Checked)  //地形图
                {
                    url += "t";
                }
                if (radioButton5.Checked)  //地形图 带标签
                {
                    url += "p";
                }
                return url;
            }
            if (radioButton6.Checked)  //国内服务器  类似 http://mt0.google.cn/vt/lyrs=m@234000000&hl=zh-CN&gl=CN&src=app&x=0&y=0&z=0
            {
                string url = "";
                url = "http://mt";
                url += serverId.ToString();
                url += ".google.cn/vt/lyrs=";

                if (radioButton1.Checked)  //路线图
                {
                    url += "m";
                }
                if (radioButton2.Checked)  //卫星图
                {
                    url += "s";
                }
                if (radioButton3.Checked)  //卫星图 带标签
                {
                    url += "y";
                }
                if (radioButton4.Checked)  //地形图
                {
                    url += "t";
                }
                if (radioButton5.Checked)  //地形图 带标签
                {
                    url += "p";
                }
                url += "@234000000&hl=zh-CN&gl=CN&src=app";
                return url;
            }
            return "";
        }
        /// <summary>
        /// 根据url下载地图
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private Bitmap DownloadImage(string url)
        {
            Bitmap bitmap = null;
            Stream stream = DownloadResource(url);
            if (stream != null)
            {
                bitmap = new Bitmap(stream);
            }

            return bitmap;
        }
        /// <summary>
        /// 利用webclient 根据url下载web资源
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public Stream DownloadResource(string url)
        {
            MemoryStream stream = null;
            try
            {
                WebClient client = new WebClient();
                client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                byte[] data = client.DownloadData(url);
                stream = new MemoryStream(data);
                client.Dispose();
            }
            catch (WebException e)
            {
            }
            return stream;
        }
        /// <summary>
        /// 下载线程方法
        /// </summary>
        /// <param name="param"></param>
        public void DownloadThreadProc(object param)
        {
            int threadId = (int)param;  //当前线程Id
            this.Invoke((Action)delegate()  //输出
            {
                rchOuput.SelectionColor = Color.Blue;
                rchOuput.AppendText(DateTime.Now.ToLongTimeString() + " 第" + threadId + "号线程开始执行\r\n");
            });
            for (int i = 0; i < _waittodownload.Count; i++)
            {
                RectInfo ri = (RectInfo)_waittodownload[i];
                if ((!ri.bComplete) &&(ri.threadId + 1 == threadId))
                {
                    try
                    {
                        string url = ri.url;
                        //根据每个图片的行、列、缩放级别  完善url
                        url += "&x=" + ri.x.ToString().Trim();  //列
                        url += "&y=" + ri.y.ToString().Trim();  //行
                        url += "&z=" + ri.z.ToString().Trim();  //缩放级别
                        Bitmap map = DownloadImage(url);
                        string file = _path + "\\" + ri.z.ToString() + "_" + ri.x.ToString() + "_" + ri.y.ToString() + ".jpg";
                        ri.Bitmap = map;
                        //文件保存格式 “缩放级别_列_行.jpg”
                        map.Save(file, System.Drawing.Imaging.ImageFormat.Jpeg);
                        
                        this.Invoke((Action)delegate()  //输出
                        {
                            rchOuput.SelectionColor = Color.Green;
                            rchOuput.AppendText(DateTime.Now.ToLongTimeString() + " 第" + threadId + "号线程下载图片" + ri.z.ToString() + "_" + ri.x.ToString() + "_" + ri.y.ToString() + ".jpg\r\n");
                        });
                        _downloadnum++;

                        ri.bComplete = true;
                    }
                    catch
                    {
                        this.Invoke((Action)delegate()  //输出
                        {
                            rchOuput.SelectionColor = Color.Red;
                            rchOuput.AppendText(DateTime.Now.ToLongTimeString() + " 第" + threadId + "号线程下载图片" + ri.z.ToString() + "_" + ri.x.ToString() + "_" + ri.y.ToString() + ".jpg失败！\r\n");
                        });
                    }
                }
            }
            this.Invoke((Action)delegate()  //输出
            {
                rchOuput.SelectionColor = Color.Blue;
                rchOuput.AppendText(DateTime.Now.ToLongTimeString() + " 第" + threadId + "号线程执行完毕\r\n" );
            });
            _thread--; //工作线程数目减一
            if (_thread == 0) //所有线程均结束
            {
                this.Invoke((Action)delegate()  //输出
                {
                    rchOuput.SelectionColor = Color.Blue;
                    rchOuput.AppendText(DateTime.Now.ToLongTimeString() + " 图片下载结束！共下载" + _downloadnum + "张，共耗时" + (DateTime.Now-_startTime).TotalSeconds + "秒");
                    groupBox1.Enabled = true;
                    button2.Enabled = true;
                    linkLabel1.Enabled = true;
                });
            }
        }
        
        /// <summary>
        /// 根据缩放级别zoom  将经纬度坐标系统中的某个点 转换成平面2D图中的点（原点在屏幕左上角）
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="zoom"></param>
        /// <returns></returns>
        public Point LatLongToPixel(double latitude, double longitude, double zoom)
        {
            Point point = new Point();

            double centerPoint = Math.Pow(2, zoom + 7);
            double totalPixels = 2 * centerPoint;
            double pixelsPerLngDegree = totalPixels / 360;
            double pixelsPerLngRadian = totalPixels / (2 * Math.PI);
            double siny = Math.Min(Math.Max(Math.Sin(latitude * (Math.PI / 180)), -0.9999), 0.9999);
            point = new Point((int)Math.Round(centerPoint + longitude * pixelsPerLngDegree), (int)Math.Round(centerPoint - 0.5 * Math.Log((1 + siny) / (1 - siny)) * pixelsPerLngRadian));

            return point;
        }
        #endregion
    }

    /// <summary>
    /// 地图中每个256*256尺寸的方块
    /// </summary>
    public class RectInfo
    {
        public int serverId;  //目标服务器
        public int threadId;  //目标下载线程
        public string url;  //下载url
        public int x;  //列
        public int y;  //行
        public int z;  //缩放级别
        public bool bComplete;  //是否完成
        public Bitmap Bitmap; //图片
    }
}
