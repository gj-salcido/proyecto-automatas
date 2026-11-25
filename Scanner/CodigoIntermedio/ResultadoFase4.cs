using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NanoRBCompiler.TablaSimbolos;

namespace NanoRBCompiler.CodigoIntermedio
{
    /// <summary>
    /// Lo que la Fase 4 entrega a la siguiente fase:
    ///   - lista de cuádruplos numerados en orden de ejecución
    ///   - tabla de temporales (t1..tN con su tipo)
    ///   - etiquetas (L1..LN) con el número del cuádruplo 'label' donde se definen
    ///   - cadenas literales (c1..cN) de los puts
    ///   - la tabla de símbolos (la misma de las fases anteriores)
    /// </summary>
    public class ResultadoFase4
    {
        public IReadOnlyList<Cuadruplo> Cuadruplos { get; }
        public TablaTemporales Temporales { get; }
        public TablaCadenas Cadenas { get; }
        public IReadOnlyDictionary<string, int> Etiquetas { get; }
        public TablaSimbolosManager Simbolos { get; }

        public ResultadoFase4(
            IReadOnlyList<Cuadruplo> cuadruplos,
            TablaTemporales temporales,
            TablaCadenas cadenas,
            IReadOnlyDictionary<string, int> etiquetas,
            TablaSimbolosManager simbolos)
        {
            Cuadruplos = cuadruplos;
            Temporales = temporales;
            Cadenas = cadenas;
            Etiquetas = etiquetas;
            Simbolos = simbolos;
        }

        public void Imprimir()
        {
            Console.WriteLine("\n=========== CUÁDRUPLOS (CÓDIGO INTERMEDIO) ===========");
            foreach (var c in Cuadruplos)
            {
                Console.WriteLine(c);
            }

            Temporales.Imprimir();
            Cadenas.Imprimir();

            Console.WriteLine("\n=========== ETIQUETAS ===========");
            if (Etiquetas.Count == 0)
            {
                Console.WriteLine("(sin etiquetas)");
            }
            foreach (var e in Etiquetas.OrderBy(e => e.Value))
            {
                Console.WriteLine($"{e.Key,-6} -> cuádruplo {e.Value}");
            }
        }

        /// <summary>Guarda los cuádruplos en un archivo de texto (.quad / .ir), una instrucción por línea.</summary>
        public void GuardarArchivo(string ruta)
        {
            File.WriteAllLines(ruta, Cuadruplos.Select(c => c.ToString()), new UTF8Encoding(false));
        }
    }
}
