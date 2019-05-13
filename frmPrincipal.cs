using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using libzkfpcsharp;
using Sample;
using System.Drawing.Imaging;
using ZKFingerSDKWindowsZK10;

namespace Demo
{
    public partial class frmPrincipal : Form
    {
        IntPtr mDevHandle = IntPtr.Zero;
        IntPtr mDBHandle = IntPtr.Zero;
        IntPtr FormHandle = IntPtr.Zero;
        Image bmp;
        Point lastPoint;
        bool bIsTimeToDie = false; 
        bool IsRegister = false;
        bool bIdentify = true;
        byte[] FPBuffer;
        int RegisterCount = 0;
        const int REGISTER_FINGER_COUNT = 3;

        byte[][] RegTmps = new byte[3][];
        byte[] RegTmp = new byte[2048];
        byte[] CapTmp = new byte[2048];

        int cbCapTmp = 2048;
        int cbRegTmp = 0;
        int iFid = 1;

        private int mfpWidth = 0;
        private int mfpHeight = 0;
        private int mfpDpi = 0;

        const int MESSAGE_CAPTURED_OK = 0x0400 + 6;

        [DllImport("user32.dll", EntryPoint = "SendMessageA")]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        public frmPrincipal()
        {
            InitializeComponent();
        }

        private void bnInit_Click(object sender, EventArgs e)
        {
            int ret = zkfperrdef.ZKFP_ERR_OK;
            if ((ret = zkfp2.Init()) == zkfperrdef.ZKFP_ERR_OK)
            {
                int nCount = zkfp2.GetDeviceCount();
                if (nCount > 0)
                {                    
                    btnIniciar.Enabled = false;
                    abrirConexion();
                }
                else
                {
                    zkfp2.Terminate();
                    MessageBox.Show("No hay dispositivo conectado!", "Sin Conexion", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
            }
            else
            {
                MessageBox.Show("Inicializacion fallida, error=" + ret + " !", "Error Inicializacion", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void abrirConexion()
        {
            int ret = zkfp.ZKFP_ERR_OK;
            if (IntPtr.Zero == (mDevHandle = zkfp2.OpenDevice(0)))
            {
                MessageBox.Show("Problema de apertura de conexion", "Error Apertura", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (IntPtr.Zero == (mDBHandle = zkfp2.DBInit()))
            {
                MessageBox.Show("Problema de inicializacion de Base de datos", "Error Base de Datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                zkfp2.CloseDevice(mDevHandle);
                mDevHandle = IntPtr.Zero;
                return;
            }
            btnIniciar.Enabled = false;
            btnEnrolar.Enabled = true;
            RegisterCount = 0;
            cbRegTmp = 0;
            iFid = 1;
            for (int i = 0; i < 3; i++)
            {
                RegTmps[i] = new byte[2048];
            }
            byte[] paramValue = new byte[4];
            int size = 4;
            zkfp2.GetParameters(mDevHandle, 1, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

            size = 4;
            zkfp2.GetParameters(mDevHandle, 2, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

            FPBuffer = new byte[mfpWidth*mfpHeight];

            size = 4;
            zkfp2.GetParameters(mDevHandle, 3, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpDpi);

            Thread captureThread = new Thread(new ThreadStart(DoCapture));
            captureThread.IsBackground = true;
            captureThread.Start();
            bIsTimeToDie = false;
            lblResult.Text = "Conexion establecida\n";

        }

        private void DoCapture()
        {
            while (!bIsTimeToDie)
            {
                cbCapTmp = 2048;
                int ret = zkfp2.AcquireFingerprint(mDevHandle, FPBuffer, CapTmp, ref cbCapTmp);
                if (ret == zkfp.ZKFP_ERR_OK)
                {
                    SendMessage(FormHandle, MESSAGE_CAPTURED_OK, IntPtr.Zero, IntPtr.Zero);
                }
                Thread.Sleep(200);
            }
        }

        protected override void DefWndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case MESSAGE_CAPTURED_OK:
                {
                        MemoryStream ms = new MemoryStream();
                        BitmapFormat.GetBitmap(FPBuffer, mfpWidth, mfpHeight, ref ms);
                        bmp = new Bitmap(ms);
                        this.picFPImg.Image = bmp;


                        String strShow = zkfp2.BlobToBase64(CapTmp, cbCapTmp);

                        if (IsRegister)
                        {
                            int ret = zkfp.ZKFP_ERR_OK;
                            int fid = 0, score = 0;
                            ret = zkfp2.DBIdentify(mDBHandle, CapTmp, ref fid, ref score);
                            if (zkfp.ZKFP_ERR_OK == ret)
                            {
                                lblResult.Text = ("This finger was already register by " + fid + "!\n");
                                return;
                            }

                            if (RegisterCount > 0 && zkfp2.DBMatch(mDBHandle, CapTmp, RegTmps[RegisterCount - 1]) <= 0)
                            {
                                lblResult.Text = ("Por favor ingrese 3 veces su dedo para el enrolamiento.\n");
                                return;
                            }

                            Array.Copy(CapTmp, RegTmps[RegisterCount], cbCapTmp);
                            String strBase64 = zkfp2.BlobToBase64(CapTmp, cbCapTmp);
                            byte[] blob = zkfp2.Base64ToBlob(strBase64);
                            RegisterCount++;
                            if (RegisterCount >= REGISTER_FINGER_COUNT)
                            {
                                RegisterCount = 0;
                                if (zkfp.ZKFP_ERR_OK == (ret = zkfp2.DBMerge(mDBHandle, RegTmps[0], RegTmps[1], RegTmps[2], RegTmp, ref cbRegTmp)) &&
                                       zkfp.ZKFP_ERR_OK == (ret = zkfp2.DBAdd(mDBHandle, iFid, RegTmp)))
                                {
                                    iFid++;
                                    var f = new frmMsgCopiar(strShow);
                                    f.ShowDialog();
                                    lblResult.Text = "Enrolamiento exitoso";
                                }
                                else
                                {
                                    lblResult.Text = ("Fracaso al enrolar, intente otra vez. Codigo de error=" + ret + "\n");
                                }
                                IsRegister = false;
                                return;
                            }
                            else
                            {
                                lblResult.Text = ("Es necesario la huella " + (REGISTER_FINGER_COUNT - RegisterCount) + " veces mas\n");
                            }
                        }
                        else
                        {
                            if (cbRegTmp <= 0)
                            {
                                lblResult.Text = ("Primero registre su dedo por favor!\n");
                                return;
                            }
                            if (bIdentify)
                            {
                                int ret = zkfp.ZKFP_ERR_OK;
                                int fid = 0, score = 0;
                                ret = zkfp2.DBIdentify(mDBHandle, CapTmp, ref fid, ref score);
                                if (zkfp.ZKFP_ERR_OK == ret)
                                {
                                    lblResult.Text = ("Identify succ, fid= " + fid + ",score=" + score + "!\n");
                                    return;
                                }
                                else
                                {
                                    lblResult.Text = ("Identify fail, ret= " + ret + "\n");
                                    return;
                                }
                            }
                            else
                            {
                                int ret = zkfp2.DBMatch(mDBHandle, CapTmp, RegTmp);
                                if (0 < ret)
                                {
                                    lblResult.Text = ("Match finger succ, score=" + ret + "!\n");
                                    return;
                                }
                                else
                                {
                                    lblResult.Text = ("Match finger fail, ret= " + ret + "\n");
                                    return;
                                }
                            }
                        }
                }
                break;

                default:
                    base.DefWndProc(ref m);
                    break;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            FormHandle = this.Handle;
        }

        private void bnEnroll_Click(object sender, EventArgs e)
        {
            if (!IsRegister)
            {
                IsRegister = true;
                RegisterCount = 0;
                cbRegTmp = 0;
                lblResult.Text = ("Por favor coloque el dedo 3 veces!\n");
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
        
        private void btnCerrar_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnMinimizar_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        public Bitmap ToGrayScale(Bitmap Bmp)
        {
            int rgb;
            Color c;

            for (int y = 0; y < Bmp.Height; y++)
                for (int x = 0; x < Bmp.Width; x++)
                {
                    c = Bmp.GetPixel(x, y);
                    rgb = (int)(Math.Round(((double)(c.R + c.G + c.B) / 3.0) / 255) * 255);
                    Bmp.SetPixel(x, y, Color.FromArgb(rgb, rgb, rgb));
                }
            return Bmp;
        }

        private void picFPImg_Paint(object sender, PaintEventArgs e)
        {
            var b = picFPImg.Image;
            if (bmp != null)
            {
                // Set the image attribute's color mappings
                ColorMap[] colorMap = new ColorMap[2];
                colorMap[0] = new ColorMap();
                colorMap[0].OldColor = Color.Black;
                colorMap[0].NewColor = Color.LightSkyBlue;
                colorMap[1] = new ColorMap();
                colorMap[1].OldColor = Color.White;
                colorMap[1].NewColor = Color.FromArgb(15, 15, 25);
                ImageAttributes attr = new ImageAttributes();
                attr.SetRemapTable(colorMap);
                // Draw using the color map
                Rectangle rect = new Rectangle(0, 0, picFPImg.Width, picFPImg.Height);
                e.Graphics.DrawImage(b, rect, 0, 0, picFPImg.Image.Width, picFPImg.Image.Height, GraphicsUnit.Pixel, attr);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var img = Image.FromFile(@"C:\Users\Nahuel Valverde\Pictures\finger.bmp");
            bmp = ToGrayScale(new Bitmap(img));
            picFPImg.Image = bmp;
            picFPImg.Focus();
        }

        #region Desplazamiento borderless form

        private void pnlTopBarra_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - lastPoint.X;
                this.Top += e.Y - lastPoint.Y;
            }
        }

        private void pnlTopBarra_MouseDown(object sender, MouseEventArgs e)
        {
            lastPoint = new Point(e.X, e.Y);
        }

        #endregion
    }
}
