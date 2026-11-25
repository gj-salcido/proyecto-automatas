using System;
using System.Collections.Generic;
using System.Globalization;
using NanoRBCompiler.TablaSimbolos;

namespace NanoRBCompiler.Semantico
{
    public enum TipoDato
    {
        Whole,   // Entero
        Dec,     // Real / Decimal
        Cadena,  // Texto entre comillas (solo válido en puts)
        Error,   // TIPO_ERROR (mecanismo anti-cascada)
        Desconocido
    }

    // --- NODOS DEL AST ---
    public abstract class NodoAST
    {
        public int Linea { get; set; }
        public TipoDato TipoResultado { get; set; } = TipoDato.Desconocido;
    }

    public class NodoLiteral : NodoAST
    {
        public string Valor { get; }
        public TipoDato TipoLiteral { get; }

        public NodoLiteral(string valor, TipoDato tipo, int linea)
        {
            Valor = valor;
            TipoLiteral = tipo;
            Linea = linea;
        }
    }

    public class NodoIdentificador : NodoAST
    {
        public string Nombre { get; }

        public NodoIdentificador(string nombre, int linea)
        {
            Nombre = nombre;
            Linea = linea;
        }
    }

    public class NodoOperacionBinaria : NodoAST
    {
        public string Operador { get; }
        public NodoAST Izquierdo { get; }
        public NodoAST Derecho { get; }

        public NodoOperacionBinaria(string operador, NodoAST izq, NodoAST der, int linea)
        {
            Operador = operador;
            Izquierdo = izq;
            Derecho = der;
            Linea = linea;
        }
    }

    public class NodoAsignacion : NodoAST
    {
        public string Variable { get; }
        public NodoAST Expresion { get; }

        public NodoAsignacion(string variable, NodoAST expresion, int linea)
        {
            Variable = variable;
            Expresion = expresion;
            Linea = linea;
        }
    }

    public class NodoCondicion : NodoAST
    {
        public string OperadorRelacional { get; }
        public NodoAST Izquierdo { get; }
        public NodoAST Derecho { get; }

        public NodoCondicion(string op, NodoAST izq, NodoAST der, int linea)
        {
            OperadorRelacional = op;
            Izquierdo = izq;
            Derecho = der;
            Linea = linea;
        }
    }

    // CAMBIO 3: la declaración ahora es un nodo del AST (whole id / dec id)
    public class NodoDeclaracion : NodoAST
    {
        public TipoDato Tipo { get; }
        public string Nombre { get; }

        public NodoDeclaracion(TipoDato tipo, string nombre, int linea)
        {
            Tipo = tipo;
            Nombre = nombre;
            Linea = linea;
        }
    }

    public class NodoEntradaSalida : NodoAST
    {
        public string Instruccion { get; } // "puts" o "gets"
        public List<NodoAST> Elementos { get; }

        public NodoEntradaSalida(string instruccion, List<NodoAST> elementos, int linea)
        {
            Instruccion = instruccion;
            Elementos = elementos ?? new List<NodoAST>();
            Linea = linea;
        }
    }

    public class NodoEstructuraControl : NodoAST
    {
        public string TipoEstructura { get; } // "if", "while"
        public NodoAST Condicion { get; }
        public List<NodoAST> Cuerpo { get; }

        // CAMBIO 2: rama alternativa de un if.
        //   - otro NodoEstructuraControl("if", ...) => representa un "elsif"
        //   - un NodoPrograma                        => representa el bloque "else"
        //   - null                                   => no hay elsif/else
        public NodoAST? Alternativa { get; }

        public NodoEstructuraControl(string tipo, NodoAST condicion, List<NodoAST> cuerpo, int linea, NodoAST? alternativa = null)
        {
            TipoEstructura = tipo;
            Condicion = condicion;
            Cuerpo = cuerpo ?? new List<NodoAST>();
            Alternativa = alternativa;
            Linea = linea;
        }
    }

    public class NodoPrograma : NodoAST
    {
        public List<NodoAST> Instrucciones { get; } = new();
    }

    // --- ANALIZADOR SEMÁNTICO ---
    public class AnalizadorSemantico
    {
        // Límites de la representación de la Fase 6 (16 bits):
        //   whole: complemento a dos        -> 0..32767 como literal (NanoRB no tiene '-' unario)
        //   dec:   punto fijo escalado x10  -> hasta 3276.7 con un decimal
        private const long MaxWhole = 32767;
        private const decimal MaxDec = 3276.7m;

        // UNA sola tabla de símbolos para todas las fases (la misma que llena el scanner).
        private readonly TablaSimbolosManager _tabla;
        private readonly List<string> _erroresSemanticos = new();
        private readonly List<string> _advertencias = new();

        public AnalizadorSemantico() : this(new TablaSimbolosManager()) { }

        public AnalizadorSemantico(TablaSimbolosManager tabla)
        {
            _tabla = tabla;
        }

        // --- Lo que necesita el generador de código ---
        public TablaSimbolosManager Tabla => _tabla;
        public bool TieneErrores => _erroresSemanticos.Count > 0;
        public int TotalErrores => _erroresSemanticos.Count;
        public IReadOnlyList<string> Errores => _erroresSemanticos;
        public IReadOnlyList<string> Advertencias => _advertencias;

        public void RegistrarDeclaracion(string nombre, TipoDato tipo, int linea)
        {
            var simbolo = _tabla.RegistrarIdentificador(nombre, 100, linea);

            // Ya tiene tipo whole/dec -> declaración doble. Se conserva el tipo original y no se duplica la entrada.
            if (simbolo.EsVariableNumerica)
            {
                ReportarError(linea, "Declaración doble", $"La variable '{nombre}' ya fue declarada anteriormente.");
                return;
            }

            // Desconocido (caso normal) o Error (se usó antes de declararla; ese error ya se reportó).
            simbolo.Tipo = tipo;
            simbolo.OrdenDeclaracion = _tabla.NuevoOrdenDeclaracion();
        }

        public TipoDato ComprobarTipos(NodoAST? nodo)
        {
            if (nodo == null) return TipoDato.Error;

            switch (nodo)
            {
                case NodoPrograma prog:
                    foreach (var inst in prog.Instrucciones) ComprobarTipos(inst);
                    return TipoDato.Desconocido;

                // La declaración se procesa al recorrer el AST, en el orden del programa
                case NodoDeclaracion decl:
                    RegistrarDeclaracion(decl.Nombre, decl.Tipo, decl.Linea);
                    decl.TipoResultado = TipoDato.Desconocido;
                    return TipoDato.Desconocido;

                case NodoLiteral lit:
                    lit.TipoResultado = lit.TipoLiteral;
                    ValidarLiteral(lit);
                    return lit.TipoResultado;

                case NodoIdentificador id:
                {
                    var simbolo = _tabla.RegistrarIdentificador(id.Nombre, 100, id.Linea);
                    if (!simbolo.TieneTipo)
                    {
                        ReportarError(id.Linea, "Variable no declarada", $"La variable '{id.Nombre}' no ha sido declarada.");
                        // ANTI-CASCADA: el símbolo queda con TipoDato.Error en la tabla; así los usos
                        // posteriores (aunque sean en otras líneas) se contienen sin reportar de nuevo.
                        simbolo.Tipo = TipoDato.Error;
                    }
                    id.TipoResultado = simbolo.Tipo;
                    return id.TipoResultado;
                }

                case NodoOperacionBinaria op:
                    TipoDato tIzq = ComprobarTipos(op.Izquierdo);
                    TipoDato tDer = ComprobarTipos(op.Derecho);

                    // ANTI-CASCADA: Si algún operando ya tenía error, se contiene silenciosamente
                    if (tIzq == TipoDato.Error || tDer == TipoDato.Error)
                    {
                        op.TipoResultado = TipoDato.Error;
                        return TipoDato.Error;
                    }

                    if (tIzq == TipoDato.Whole && tDer == TipoDato.Whole)
                    {
                        op.TipoResultado = TipoDato.Whole;
                    }
                    else if ((tIzq == TipoDato.Whole && tDer == TipoDato.Dec) ||
                             (tIzq == TipoDato.Dec && tDer == TipoDato.Whole) ||
                             (tIzq == TipoDato.Dec && tDer == TipoDato.Dec))
                    {
                        op.TipoResultado = TipoDato.Dec; // Promoción
                    }
                    else
                    {
                        ReportarError(op.Linea, "Tipos incompatibles", $"No se puede realizar la operación '{op.Operador}' entre {tIzq} y {tDer}.");
                        op.TipoResultado = TipoDato.Error;
                        return op.TipoResultado;
                    }

                    // División entre un literal cero: el 8086 dispararía INT 0, mejor detectarlo en compilación.
                    // (La división por una VARIABLE que vale 0 sigue siendo un error de ejecución.)
                    if (op.Operador == "/" && op.Derecho is NodoLiteral divisor && EsLiteralCero(divisor))
                    {
                        ReportarError(op.Linea, "División entre cero", "El divisor es el literal 0.");
                        op.TipoResultado = TipoDato.Error;
                    }
                    return op.TipoResultado;

                case NodoAsignacion asig:
                {
                    TipoDato tipoExpr = ComprobarTipos(asig.Expresion);

                    var simbolo = _tabla.RegistrarIdentificador(asig.Variable, 100, asig.Linea);
                    if (!simbolo.TieneTipo)
                    {
                        ReportarError(asig.Linea, "Variable no declarada", $"No se puede asignar a la variable no declarada '{asig.Variable}'.");
                        simbolo.Tipo = TipoDato.Error; // ANTI-CASCADA (ver NodoIdentificador)
                        asig.TipoResultado = TipoDato.Error;
                        return TipoDato.Error;
                    }

                    TipoDato tipoVar = simbolo.Tipo;

                    // ANTI-CASCADA: variable o expresión ya erróneas -> no emitir error secundario
                    if (tipoVar == TipoDato.Error || tipoExpr == TipoDato.Error)
                    {
                        asig.TipoResultado = TipoDato.Error;
                        return TipoDato.Error;
                    }

                    // Regla de Asignación: dec -> whole proscripto (decisión del equipo)
                    if (tipoVar == TipoDato.Whole && tipoExpr == TipoDato.Dec)
                    {
                        ReportarError(asig.Linea, "Tipos incompatibles", $"No se permite asignar un valor real ('dec') a la variable entera 'whole {asig.Variable}'.");
                        asig.TipoResultado = TipoDato.Error;
                        return TipoDato.Error;
                    }

                    // tipoVar ya es Whole o Dec (Error y Desconocido se descartaron arriba)
                    if (tipoExpr == TipoDato.Cadena)
                    {
                        ReportarError(asig.Linea, "Tipos incompatibles", $"No se puede asignar una cadena de texto a una variable numérica '{asig.Variable}'.");
                        asig.TipoResultado = TipoDato.Error;
                        return TipoDato.Error;
                    }

                    // whole -> dec es válido (promoción). El generador de código inserta int2float.
                    asig.TipoResultado = tipoVar;
                    return tipoVar;
                }

                case NodoCondicion cond:
                    TipoDato cIzq = ComprobarTipos(cond.Izquierdo);
                    TipoDato cDer = ComprobarTipos(cond.Derecho);

                    if (cIzq == TipoDato.Error || cDer == TipoDato.Error)
                    {
                        cond.TipoResultado = TipoDato.Error;
                        return TipoDato.Error;
                    }

                    if ((cIzq == TipoDato.Whole || cIzq == TipoDato.Dec) &&
                        (cDer == TipoDato.Whole || cDer == TipoDato.Dec))
                    {
                        cond.TipoResultado = TipoDato.Whole; // Condición válida
                        return TipoDato.Whole;
                    }

                    ReportarError(cond.Linea, "Tipos incompatibles", $"No se pueden comparar tipos {cIzq} y {cDer} en la condición.");
                    cond.TipoResultado = TipoDato.Error;
                    return TipoDato.Error;

                case NodoEntradaSalida es:
                    foreach (var elem in es.Elementos)
                    {
                        // El identificador se visita una sola vez y ahí mismo se reporta si no está declarado.
                        if (es.Instruccion == "gets" && elem is not NodoIdentificador)
                            ReportarError(es.Linea, "Entrada inválida", "gets requiere el nombre de una variable.");
                        else
                            ComprobarTipos(elem);
                    }
                    return TipoDato.Desconocido;

                case NodoEstructuraControl ctrl:
                    ComprobarTipos(ctrl.Condicion);
                    foreach (var inst in ctrl.Cuerpo) ComprobarTipos(inst);
                    // Se visita la alternativa (elsif encadenado o bloque else)
                    if (ctrl.Alternativa != null) ComprobarTipos(ctrl.Alternativa);
                    return TipoDato.Desconocido;

                default:
                    // Un nodo desconocido no falla en silencio
                    ReportarError(nodo.Linea, "Nodo no soportado", $"El analizador semántico no reconoce el nodo '{nodo.GetType().Name}'.");
                    return TipoDato.Error;
            }
        }

        // ------------------------------------------------------------------
        //  Validación de literales según la representación de 16 bits (Fase 6)
        // ------------------------------------------------------------------
        private void ValidarLiteral(NodoLiteral lit)
        {
            if (lit.TipoLiteral == TipoDato.Whole)
            {
                if (!long.TryParse(lit.Valor, NumberStyles.None, CultureInfo.InvariantCulture, out long v) || v > MaxWhole)
                {
                    ReportarError(lit.Linea, "Literal fuera de rango", $"El entero '{lit.Valor}' excede el máximo de {MaxWhole} (whole es de 16 bits).");
                    lit.TipoResultado = TipoDato.Error;
                }
            }
            else if (lit.TipoLiteral == TipoDato.Dec)
            {
                // Se compara el valor ya truncado a un decimal: 3276.75 se guarda como 3276.7 y sí cabe.
                if (!decimal.TryParse(lit.Valor, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d)
                    || decimal.Truncate(d * 10m) / 10m > MaxDec)
                {
                    ReportarError(lit.Linea, "Literal fuera de rango", $"El real '{lit.Valor}' excede el máximo de {MaxDec.ToString(CultureInfo.InvariantCulture)} (dec usa punto fijo x10 en 16 bits).");
                    lit.TipoResultado = TipoDato.Error;
                }
                else if (d * 10m != decimal.Truncate(d * 10m))
                {
                    // No bloquea la compilación: el generador trunca al primer decimal.
                    decimal truncado = decimal.Truncate(d * 10m) / 10m;
                    ReportarAdvertencia(lit.Linea, $"El real '{lit.Valor}' tiene más de un decimal; dec solo guarda uno (se usará {truncado.ToString("0.0", CultureInfo.InvariantCulture)}).");
                }
            }
        }

        private static bool EsLiteralCero(NodoLiteral lit)
        {
            if (lit.TipoLiteral != TipoDato.Whole && lit.TipoLiteral != TipoDato.Dec) return false;
            return decimal.TryParse(lit.Valor, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d) && d == 0m;
        }

        private void ReportarError(int linea, string tipo, string mensaje)
        {
            string err = $"[Línea {linea,2}] Error Semántico ({tipo}): {mensaje}";
            _erroresSemanticos.Add(err);
        }

        private void ReportarAdvertencia(int linea, string mensaje)
        {
            _advertencias.Add($"[Línea {linea,2}] Advertencia: {mensaje}");
        }

        public void ImprimirErrores()
        {
            Console.WriteLine("================ REPORTE DE ERRORES SEMÁNTICOS ================");
            if (_erroresSemanticos.Count == 0)
            {
                Console.WriteLine("No se encontraron errores semánticos.");
            }
            else
            {
                foreach (var err in _erroresSemanticos)
                {
                    Console.WriteLine(err);
                }
            }

            foreach (var adv in _advertencias)
            {
                Console.WriteLine(adv);
            }

            Console.WriteLine($"\nTotal de errores semánticos detectados: {_erroresSemanticos.Count}");
        }
    }
}
