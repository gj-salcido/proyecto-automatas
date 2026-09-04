using System;
using System.Collections.Generic;

namespace NanoRBCompiler.TablaSimbolos
{
    public class Simbolo
    {
        public string Lexema { get; set; }
        public int CodigoToken { get; set; }
        public int LineaPrimeraAparicion { get; set; }
        public string TipoDato { get; set; } // Pendiente para fase semántica
        public string Scope { get; set; }    // Pendiente para fase semántica
        public int OffsetMemoria { get; set; } // Pendiente para código objeto

        public Simbolo(string lexema, int codigoToken, int linea)
        {
            Lexema = lexema;
            CodigoToken = codigoToken;
            LineaPrimeraAparicion = linea;
            TipoDato = "PENDIENTE";
            Scope = "PENDIENTE";
            OffsetMemoria = -1;
        }
    }

    public class TablaSimbolosManager
    {
        private readonly Dictionary<string, Simbolo> _tabla = new();

        public void RegistrarIdentificador(string lexema, int codigoToken, int linea)
        {
            if (!_tabla.ContainsKey(lexema))
            {
                _tabla[lexema] = new Simbolo(lexema, codigoToken, linea);
            }
        }

        public void ImprimirTabla()
        {
            Console.WriteLine("\n================================ TABLA DE SÍMBOLOS ================================");
            Console.WriteLine($"{"Lexema",-15} | {"Token",-6} | {"Línea",-6} | {"Tipo (Semántica)",-18} | {"Scope (Semántica)",-18}");
            Console.WriteLine(new string('-', 75));

            foreach (var kvp in _tabla)
            {
                var s = kvp.Value;
                Console.WriteLine($"{s.Lexema,-15} | {s.CodigoToken,-6} | {s.LineaPrimeraAparicion,-6} | {s.TipoDato,-18} | {s.Scope,-18}");
            }
            Console.WriteLine("===================================================================================\n");
        }
    }
}