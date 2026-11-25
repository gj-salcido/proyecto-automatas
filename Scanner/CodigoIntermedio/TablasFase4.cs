using System;
using System.Collections.Generic;
using NanoRBCompiler.Semantico;
using NanoRBCompiler.TablaSimbolos;

namespace NanoRBCompiler.CodigoIntermedio
{
    // ------------------------------------------------------------------
    //  Temporales: t1, t2, t3... con su tipo (la Fase 6 los necesita para saber cómo operarlos y mostrarlos)
    // ------------------------------------------------------------------
    public class Temporal
    {
        public string Nombre { get; }
        public TipoDato Tipo { get; }
        public int OffsetMemoria { get; set; } = -1;   // lo asigna AsignarOffsets, después de las variables

        public Temporal(string nombre, TipoDato tipo)
        {
            Nombre = nombre;
            Tipo = tipo;
        }
    }

    public class TablaTemporales
    {
        private readonly TablaSimbolosManager _simbolos;
        private readonly List<Temporal> _lista = new();
        private int _contador = 0;

        public TablaTemporales(TablaSimbolosManager simbolos)
        {
            _simbolos = simbolos;
        }

        public IReadOnlyList<Temporal> Temporales => _lista;

        /// <summary>Bytes que ocupan los temporales (2 por cada uno); se calcula en AsignarOffsets.</summary>
        public int BytesTemporales { get; private set; }

        /// <summary>
        /// Crea el siguiente temporal. "t1" es un identificador válido de NanoRB: si el usuario ya declaró
        /// una variable con ese nombre, se salta para que los cuádruplos no sean ambiguos.
        /// </summary>
        public Temporal Nuevo(TipoDato tipo)
        {
            string nombre;
            do
            {
                _contador++;
                nombre = "t" + _contador;
            }
            while (_simbolos.TryObtener(nombre, out _));

            var temporal = new Temporal(nombre, tipo);
            _lista.Add(temporal);
            return temporal;
        }

        /// <summary>Coloca los temporales en .DATA a partir de 'inicio' (normalmente BytesVariables). Todos son de 2 bytes.</summary>
        public void AsignarOffsets(int inicio)
        {
            int offset = inicio;
            foreach (var t in _lista)
            {
                t.OffsetMemoria = offset;
                offset += 2;
            }
            BytesTemporales = offset - inicio;
        }

        public void Imprimir()
        {
            Console.WriteLine("\n=========== TABLA DE TEMPORALES ===========");
            Console.WriteLine($"{"Nombre",-8} | {"Tipo",-8} | {"Offset",-6}");
            Console.WriteLine(new string('-', 30));
            foreach (var t in _lista)
            {
                string offset = t.OffsetMemoria >= 0 ? $"{t.OffsetMemoria:X4}h" : "-";
                Console.WriteLine($"{t.Nombre,-8} | {t.Tipo,-8} | {offset,-6}");
            }
            if (_lista.Count == 0) Console.WriteLine("(sin temporales)");
        }
    }

    // ------------------------------------------------------------------
    //  Cadenas literales de los puts: c1, c2... para la zona de datos de la Fase 6
    // ------------------------------------------------------------------
    public class CadenaLiteral
    {
        public string Nombre { get; }
        public string Texto { get; }   // sin comillas; el salto de línea del puts es el carácter '\n'

        public CadenaLiteral(string nombre, string texto)
        {
            Nombre = nombre;
            Texto = texto;
        }
    }

    public class TablaCadenas
    {
        private readonly TablaSimbolosManager _simbolos;
        private readonly List<CadenaLiteral> _lista = new();
        private readonly Dictionary<string, CadenaLiteral> _porTexto = new(StringComparer.Ordinal);
        private int _contador = 0;

        public TablaCadenas(TablaSimbolosManager simbolos)
        {
            _simbolos = simbolos;
        }

        public IReadOnlyList<CadenaLiteral> Cadenas => _lista;

        /// <summary>Devuelve la cadena con ese texto; si no existe la crea. Los textos repetidos comparten nombre.</summary>
        public CadenaLiteral Obtener(string texto)
        {
            if (!_porTexto.TryGetValue(texto, out CadenaLiteral? cadena))
            {
                string nombre;
                do
                {
                    _contador++;
                    nombre = "c" + _contador;
                }
                while (_simbolos.TryObtener(nombre, out _));   // mismo criterio que los temporales

                cadena = new CadenaLiteral(nombre, texto);
                _porTexto[texto] = cadena;
                _lista.Add(cadena);
            }
            return cadena;
        }

        // Para mostrar: el salto de línea real se ve como \n
        public static string Escapar(string texto)
        {
            return texto.Replace("\\", "\\\\").Replace("\n", "\\n");
        }

        public void Imprimir()
        {
            Console.WriteLine("\n=========== CADENAS LITERALES ===========");
            Console.WriteLine($"{"Nombre",-8} | Texto");
            Console.WriteLine(new string('-', 30));
            foreach (var c in _lista)
            {
                Console.WriteLine($"{c.Nombre,-8} | \"{Escapar(c.Texto)}\"");
            }
            if (_lista.Count == 0) Console.WriteLine("(sin cadenas)");
        }
    }
}
