using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NanoRBCompiler.Semantico;

namespace NanoRBCompiler.TablaSimbolos
{
    /// <summary>
    /// Entrada de la tabla de símbolos. Una sola tabla sirve a las tres fases:
    ///   Fase 1 (léxico):    Lexema, CodigoToken, Lineas
    ///   Fase 3 (semántica): Tipo, Scope
    ///   Fase 6 (objeto):    OffsetMemoria, NombreAsm
    /// </summary>
    public class Simbolo
    {
        public string Lexema { get; }
        public int CodigoToken { get; }
        public int LineaPrimeraAparicion { get; }

        /// <summary>Todas las líneas donde aparece el identificador (Fase 3, Ejercicio 1).</summary>
        public List<int> Lineas { get; } = new();

        // --- Fase 3 ---
        /// <summary>Desconocido = todavía sin declarar. Error = uso sin declarar ya reportado (anti-cascada).</summary>
        public TipoDato Tipo { get; set; } = TipoDato.Desconocido;

        /// <summary>NanoRB tiene un solo ámbito global (decisión a documentar en la Fase 3).</summary>
        public string Scope { get; set; } = "global";

        public int OrdenDeclaracion { get; set; } = -1;

        // --- Fase 6 ---
        public int OffsetMemoria { get; set; } = -1;
        public string NombreAsm { get; set; } = string.Empty;

        public bool TieneTipo => Tipo != TipoDato.Desconocido;
        public bool EsVariableNumerica => Tipo == TipoDato.Whole || Tipo == TipoDato.Dec;

        public Simbolo(string lexema, int codigoToken, int linea)
        {
            Lexema = lexema;
            CodigoToken = codigoToken;
            LineaPrimeraAparicion = linea;
            Lineas.Add(linea);
        }
    }

    public class TablaSimbolosManager
    {
        // Clave = lexema tal cual (NanoRB distingue mayúsculas de minúsculas).
        private readonly Dictionary<string, Simbolo> _tabla = new(StringComparer.Ordinal);
        private readonly List<Simbolo> _orden = new();
        private int _siguienteOrden = 0;

        /// <summary>Símbolos en orden de primera aparición.</summary>
        public IReadOnlyList<Simbolo> Simbolos => _orden;

        /// <summary>Bytes que ocupan las variables de usuario en .DATA (se calcula en AsignarOffsets).</summary>
        public int BytesVariables { get; private set; }

        /// <summary>
        /// Registra el identificador si no existe y anota la línea donde aparece.
        /// Lo llaman el scanner (al reconocer el token 100) y el analizador semántico.
        /// </summary>
        public Simbolo RegistrarIdentificador(string lexema, int codigoToken, int linea)
        {
            if (!_tabla.TryGetValue(lexema, out var simbolo))
            {
                simbolo = new Simbolo(lexema, codigoToken, linea);
                _tabla[lexema] = simbolo;
                _orden.Add(simbolo);
            }
            else if (!simbolo.Lineas.Contains(linea))
            {
                simbolo.Lineas.Add(linea);
            }
            return simbolo;
        }

        public bool TryObtener(string lexema, out Simbolo? simbolo)
        {
            return _tabla.TryGetValue(lexema, out simbolo);
        }

        public int NuevoOrdenDeclaracion() => _siguienteOrden++;

        // ------------------------------------------------------------------
        //  Fase 6: offsets y nombres para el ensamblador
        // ------------------------------------------------------------------

        /// <summary>
        /// Asigna a cada variable declarada (whole/dec) su desplazamiento en .DATA y su nombre en ASM.
        /// Ambos tipos ocupan 2 bytes (DW). El orden es el de declaración, así que el primer
        /// símbolo queda en 0000h, el segundo en 0002h, etc. (igual que el Ejercicio 2 de la Fase 6).
        /// Los temporales t1..tN se colocan DESPUÉS de BytesVariables.
        /// </summary>
        public void AsignarOffsets()
        {
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // el ensamblador no distingue mayúsculas
            int offset = 0;

            foreach (var s in _orden.Where(x => x.EsVariableNumerica).OrderBy(x => x.OrdenDeclaracion))
            {
                s.OffsetMemoria = offset;
                s.NombreAsm = GenerarNombreAsm(s.Lexema, usados);
                offset += 2;
            }
            BytesVariables = offset;
        }

        /// <summary>
        /// v_&lt;id&gt;. Si dos identificadores solo difieren en mayúsculas (Total / total) o llevan
        /// caracteres que el ensamblador no acepta, se agrega un sufijo para que sean únicos.
        /// </summary>
        public static string GenerarNombreAsm(string lexema, HashSet<string> usados)
        {
            var sb = new StringBuilder("v_");
            foreach (char c in lexema)
            {
                bool ascii = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (ascii) sb.Append(c);
                else sb.Append('x').Append(((int)c).ToString("X4"));
            }

            string baseNombre = sb.ToString();
            string nombre = baseNombre;
            int n = 2;
            while (!usados.Add(nombre))
            {
                nombre = $"{baseNombre}_{n}";
                n++;
            }
            return nombre;
        }

        public void ImprimirTabla()
        {
            Console.WriteLine("\n======================================= TABLA DE SÍMBOLOS =======================================");
            Console.WriteLine($"{"Idx",-3} | {"Lexema",-12} | {"Tipo",-8} | {"Token",-5} | {"Línea(s)",-18} | {"Ámbito",-7} | {"Offset",-6} | {"Nombre ASM",-12}");
            Console.WriteLine(new string('-', 105));

            for (int i = 0; i < _orden.Count; i++)
            {
                var s = _orden[i];
                string lineas = string.Join(", ", s.Lineas.OrderBy(l => l));
                string offset = s.OffsetMemoria >= 0 ? $"{s.OffsetMemoria:X4}h" : "-";
                string nombreAsm = string.IsNullOrEmpty(s.NombreAsm) ? "-" : s.NombreAsm;
                string tipo = s.TieneTipo ? s.Tipo.ToString() : "-";

                Console.WriteLine($"{i,-3} | {s.Lexema,-12} | {tipo,-8} | {s.CodigoToken,-5} | {lineas,-18} | {s.Scope,-7} | {offset,-6} | {nombreAsm,-12}");
            }
            Console.WriteLine(new string('=', 105) + "\n");
        }
    }
}
