using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TostadoPersistentKit;

namespace Reflection
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PersonaRepository repoPersona = new PersonaRepository();

            Persona persona = repoPersona.traerPersonaCualquiera();

            nameTextbox.Text = persona.nombre;
            ageTextbox.Text = persona.edad.ToString();

            if (typeof(Serializable).IsAssignableFrom(typeof(Persona)))
            {
                MessageBox.Show("Persona implementa Serializable!!");
            }
            
        }
    }
}
