using System;
using System.Collections.Generic;

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
        private readonly Dictionary<string, TipoDato> _tablaSimbolos = new();
        private readonly List<string> _erroresSemanticos = new();

        public void RegistrarDeclaracion(string nombre, TipoDato tipo, int linea)
        {
            if (_tablaSimbolos.ContainsKey(nombre))
            {
                ReportarError(linea, "Declaración doble", $"La variable '{nombre}' ya fue declarada anteriormente.");
                return;
            }
            _tablaSimbolos[nombre] = tipo;
        }

        public TipoDato ComprobarTipos(NodoAST? nodo)
        {
            if (nodo == null) return TipoDato.Error;

            switch (nodo)
            {
                case NodoPrograma prog:
                    foreach (var inst in prog.Instrucciones) ComprobarTipos(inst);
                    return TipoDato.Desconocido;

                // CAMBIO 3: la declaración se procesa al recorrer el AST, en el orden del programa
                case NodoDeclaracion decl:
                    RegistrarDeclaracion(decl.Nombre, decl.Tipo, decl.Linea);
                    decl.TipoResultado = TipoDato.Desconocido;
                    return TipoDato.Desconocido;

                case NodoLiteral lit:
                    lit.TipoResultado = lit.TipoLiteral;
                    return lit.TipoResultado;

                case NodoIdentificador id:
                    if (!_tablaSimbolos.ContainsKey(id.Nombre))
                    {
                        ReportarError(id.Linea, "Variable no declarada", $"La variable '{id.Nombre}' no ha sido declarada.");
                        id.TipoResultado = TipoDato.Error; // <-- MECANISMO ANTI-CASCADA
                        return TipoDato.Error;
                    }
                    id.TipoResultado = _tablaSimbolos[id.Nombre];
                    return id.TipoResultado;

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
                    }
                    return op.TipoResultado;

                case NodoAsignacion asig:
                    TipoDato tipoExpr = ComprobarTipos(asig.Expresion);

                    if (!_tablaSimbolos.ContainsKey(asig.Variable))
                    {
                        ReportarError(asig.Linea, "Variable no declarada", $"No se puede asignar a la variable no declarada '{asig.Variable}'.");
                        asig.TipoResultado = TipoDato.Error;
                        return TipoDato.Error;
                    }

                    TipoDato tipoVar = _tablaSimbolos[asig.Variable];

                    // ANTI-CASCADA: Si la expresión traía error, no emitir error secundario en la asignación
                    if (tipoExpr == TipoDato.Error)
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

                    if (tipoVar == TipoDato.Whole && tipoExpr == TipoDato.Cadena ||
                        tipoVar == TipoDato.Dec && tipoExpr == TipoDato.Cadena)
                    {
                        ReportarError(asig.Linea, "Tipos incompatibles", $"No se puede asignar una cadena de texto a una variable numérica '{asig.Variable}'.");
                        asig.TipoResultado = TipoDato.Error;
                        return TipoDato.Error;
                    }

                    asig.TipoResultado = tipoVar;
                    return tipoVar;

                case NodoCondicion cond:
                    TipoDato cIzq = ComprobarTipos(cond.Izquierdo);
                    TipoDato cDer = ComprobarTipos(cond.Derecho);

                    if (cIzq == TipoDato.Error || cDer == TipoDato.Error)
                    {
                        cond.TipoResultado = TipoDato.Error; // CAMBIO 5: ahora también se guarda en el nodo
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
                        // CAMBIO 1: antes, gets reportaba dos veces la misma variable no declarada
                        // (una al visitar el identificador y otra en un "if" repetido).
                        // Ahora el identificador se visita una sola vez y ahí mismo se reporta.
                        if (es.Instruccion == "gets" && elem is not NodoIdentificador)
                            ReportarError(es.Linea, "Entrada inválida", "gets requiere el nombre de una variable.");
                        else
                            ComprobarTipos(elem);
                    }
                    return TipoDato.Desconocido;

                case NodoEstructuraControl ctrl:
                    ComprobarTipos(ctrl.Condicion);
                    foreach (var inst in ctrl.Cuerpo) ComprobarTipos(inst);
                    // CAMBIO 2: se visita la alternativa (elsif encadenado o bloque else)
                    if (ctrl.Alternativa != null) ComprobarTipos(ctrl.Alternativa);
                    return TipoDato.Desconocido;

                default:
                    // CAMBIO 4: un nodo desconocido ya no falla en silencio
                    ReportarError(nodo.Linea, "Nodo no soportado", $"El analizador semántico no reconoce el nodo '{nodo.GetType().Name}'.");
                    return TipoDato.Error;
            }
        }

        private void ReportarError(int linea, string tipo, string mensaje)
        {
            string err = $"[Línea {linea,2}] Error Semántico ({tipo}): {mensaje}";
            _erroresSemanticos.Add(err);
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
            Console.WriteLine($"\nTotal de errores semánticos detectados: {_erroresSemanticos.Count}");
        }
    }
}
