using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ZKFingerSDKWindowsZK10
{
    public partial class frmMsgCopiar : Form
    {
        String cod;
        public frmMsgCopiar(string codigo)
        {
            cod = codigo;
            InitializeComponent();
        }

        private void btnCopiar_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(cod);
            this.Close();
        }
    }
}
