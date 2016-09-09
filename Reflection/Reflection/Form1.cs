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
        public Persona persona;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            PersonaRepository repoPersona = new PersonaRepository();

            persona = repoPersona.traerPersonaCualquiera();

            Persona personaAInsertar = new Persona();

            personaAInsertar.nombre = "fernimanco";
            personaAInsertar.dni = 123314;
            personaAInsertar.edad = 22;

            repoPersona.insert(personaAInsertar, "persona");

            List<Serializable> personas = repoPersona.selectAll("persona");

            //textBox.DataBindings.Add("Text", obj, "SomeProperty");

            //nameTextbox.DataBindings.Add("Text", persona, "nombre");

            Binding binding = new Binding("Text", persona, "nombre");
            binding.ControlUpdateMode = ControlUpdateMode.OnPropertyChanged;
            binding.DataSourceUpdateMode = DataSourceUpdateMode.OnPropertyChanged;

            nameTextbox.DataBindings.Add(binding);


            persona.nombre = "otro nombre";
            //textBox.DataBindings["textBoxProperty"].WriteValue();
            //nameTextbox.DataBindings["Text"].WriteValue();

            //nameTextbox.Text = persona.humano.nombreHumano;//persona.nombre;
            //ageTextbox.Text = persona.humano.dni.ToString();//edad.ToString();

            if (typeof(Serializable).IsAssignableFrom(typeof(Persona)))
            {
                //MessageBox.Show("Persona implementa Serializable!!");
            }

            //MessageBox.Show("El dni es: " +persona.humano.dni.ToString());

            //MessageBox.Show("El nombre de humano es: "+persona.nombre);//humano.nombreHumano.ToString());

        }

        private void button2_Click(object sender, EventArgs e)
        {
            MessageBox.Show(persona.nombre);
        }
    }
}
