using System;
using System.Collections.Generic;

namespace NanoRBCompiler.Errores
{
    public class ErrorLexico
    {
        public int CodigoError { get; }
        public string Mensaje { get; }
        public int Linea { get; }
        public string Lexema { get; }

        public ErrorLexico(int codigoError, string mensaje, int linea, string lexema)
        {
            CodigoError = codigoError;
            Mensaje = mensaje;
            Linea = linea;
            Lexema = lexema;
        }

        public override string ToString()
        {
            return $"[ERROR LÉXICO {CodigoError}] Línea {Linea}: {Mensaje} (Lexema: \"{Lexema}\")";
        }
    }

    public class ManejadorErrores
    {
        private readonly List<ErrorLexico> _listaErrores = new();

        public void ReportarError(int codigo, string mensaje, int linea, string lexema)
        {
            _listaErrores.Add(new ErrorLexico(codigo, mensaje, linea, lexema));
        }

        public bool TieneErrores => _listaErrores.Count > 0;

        public void ImprimirErrores()
        {
            if (!TieneErrores)
            {
                Console.WriteLine("\n--> Análisis léxico completado sin errores.");
                return;
            }

            Console.WriteLine("\n================================ REPORTE DE ERRORES LÉXICOS ================================");
            foreach (var err in _listaErrores)
            {
                Console.WriteLine(err);
            }
            Console.WriteLine("============================================================================================\n");
        }
    }
}