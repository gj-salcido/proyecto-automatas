using System;
using System.Collections.Generic;
using NanoRBCompiler.Scanner;
using NanoRBCompiler.Semantico;

namespace NanoRBCompiler.Parser
{
    /// <summary>
    /// Parser de descenso recursivo para NanoRB (Fase 2). Recibe la lista de tokens del scanner
    /// y construye el AST que consume el analizador semántico.
    ///
    /// Gramática (LL(1), sin recursión izquierda; EXPRESION' y TERMINO' se implementan como ciclos while,
    /// con asociatividad izquierda):
    ///   PROGRAMA     -> start SALTO LINEAS finish
    ///   LINEA        -> INSTRUCCION SALTO | SALTO
    ///   INSTRUCCION  -> DECLARACION | ASIGNACION | ENTRADA | SALIDA | CONDICIONAL | CICLO_WHILE
    ///   DECLARACION  -> (whole | dec) id
    ///   ASIGNACION   -> id = EXPRESION
    ///   ENTRADA      -> gets id
    ///   SALIDA       -> puts ELEM_SALIDA { , ELEM_SALIDA }
    ///   ELEM_SALIDA  -> cadena | EXPRESION
    ///   CONDICIONAL  -> if CONDICION SALTO LINEAS { elsif CONDICION SALTO LINEAS } [ else SALTO LINEAS ] end
    ///   CICLO_WHILE  -> while CONDICION SALTO LINEAS end
    ///   CONDICION    -> EXPRESION OP_REL EXPRESION
    ///   EXPRESION    -> TERMINO { (+ | -) TERMINO }
    ///   TERMINO      -> FACTOR { (* | /) FACTOR }
    ///   FACTOR       -> ( EXPRESION ) | id | num_entero | num_real
    ///
    /// Recuperación de errores: modo pánico. Ante un error se reporta, se descarta lo que queda de la
    /// línea (hasta el SALTO) y se sigue con la siguiente instrucción, para encontrar varios errores por corrida.
    /// Los tokens de error léxico (500, 501, 503) ya los reportó el scanner: aquí solo provocan la recuperación,
    /// sin reportar un segundo error por lo mismo.
    /// </summary>
    public class ParserEngine
    {
        // Códigos de token (Ejercicio 1)
        private const int TkEof = 0;
        private const int TkId = 100;
        private const int TkEntero = 101;
        private const int TkReal = 102;
        private const int TkCadena = 103;
        private const int TkAsigna = 104;
        private const int TkRelPrimero = 105;   // ==
        private const int TkRelUltimo = 110;    // >=   (105..110 son los 6 operadores relacionales)
        private const int TkMas = 111;
        private const int TkMenos = 112;
        private const int TkPor = 113;
        private const int TkEntre = 114;
        private const int TkParAbre = 115;
        private const int TkParCierra = 116;
        private const int TkComa = 117;
        private const int TkSalto = 118;
        private const int TkStart = 200;
        private const int TkFinish = 201;
        private const int TkWhole = 202;
        private const int TkDec = 203;
        private const int TkIf = 204;
        private const int TkElsif = 205;
        private const int TkElse = 206;
        private const int TkEnd = 207;
        private const int TkWhile = 208;
        private const int TkGets = 209;
        private const int TkPuts = 210;
        private const int TkPrimerError = 500;  // 500, 501, 503: errores léxicos ya reportados

        // Excepción interna para saltar a la recuperación; nunca sale de esta clase.
        private sealed class ErrorSintactico : Exception { }

        private readonly List<Token> _tokens;
        private readonly List<string> _errores = new();
        private readonly bool _cadenaComoOperando;
        private int _pos;
        private bool _finDeLineaOmitido;   // se reportó un 'end' faltante: no exigir además el SALTO

        /// <param name="aceptarCadenaComoOperando">
        /// La gramática estricta solo permite cadenas en 'puts'. Con true (por defecto) el parser también
        /// acepta una cadena como operando en cualquier expresión, y es el analizador semántico quien reporta
        /// "Tipos incompatibles" (así el Ejercicio 5, total = "hola", sigue siendo el error semántico 3).
        /// Con false se aplica la gramática al pie de la letra y ese caso es un error sintáctico.
        /// </param>
        public ParserEngine(List<Token> tokens, bool aceptarCadenaComoOperando = true)
        {
            _tokens = new List<Token>(tokens ?? new List<Token>());
            int ultimaLinea = _tokens.Count > 0 ? _tokens[_tokens.Count - 1].Linea : 1;
            _tokens.Add(new Token(TkEof, "EOF", ultimaLinea));   // centinela: el scanner no devuelve EOF en la lista
            _cadenaComoOperando = aceptarCadenaComoOperando;
        }

        public bool TieneErrores => _errores.Count > 0;
        public int TotalErrores => _errores.Count;
        public IReadOnlyList<string> Errores => _errores;

        private Token Actual => _tokens[_pos];

        // ------------------------------------------------------------------
        //  PROGRAMA
        // ------------------------------------------------------------------
        public NodoPrograma Analizar()
        {
            _pos = 0;
            _errores.Clear();
            _finDeLineaOmitido = false;

            var programa = new NodoPrograma();

            // Se toleran SALTOs (líneas en blanco o de comentario) antes de 'start' y después de 'finish'.
            SaltarSaltos();
            if (Actual.Codigo == TkStart)
            {
                programa.Linea = Avanzar().Linea;
                ExigirSaltoEnEncabezado("después de 'start'");
            }
            else
            {
                programa.Linea = Actual.Linea;
                ReportarError(Actual.Linea, $"El programa debe comenzar con 'start', pero se encontró {Describir(Actual)}.");
            }

            while (true)
            {
                ParseBloque(programa.Instrucciones);
                if (Actual.Codigo == TkFinish || Actual.Codigo == TkEof) break;

                // Quedó un end/else/elsif que no cierra ni continúa nada
                string que = Actual.Codigo == TkEnd ? "'end' sin un if o while abierto" : $"'{Actual.Lexema}' sin un if abierto";
                ReportarError(Actual.Linea, $"{que}.");
                Sincronizar();
            }

            if (Actual.Codigo == TkFinish)
            {
                Avanzar();
                SaltarSaltos();
                if (Actual.Codigo != TkEof)
                    ReportarError(Actual.Linea, $"Hay código después de 'finish' (empieza en {Describir(Actual)}).");
            }
            else
            {
                ReportarError(Actual.Linea, "Se esperaba 'finish' al final del programa.");
            }

            return programa;
        }

        // ------------------------------------------------------------------
        //  LINEAS: se detiene en end / else / elsif / finish / EOF (lo decide quien llamó)
        // ------------------------------------------------------------------
        private void ParseBloque(List<NodoAST> destino)
        {
            while (true)
            {
                SaltarSaltos();
                if (EsFinDeBloque()) return;

                try
                {
                    NodoAST sentencia = ParseSentencia();
                    destino.Add(sentencia);

                    // LINEA -> INSTRUCCION SALTO: la instrucción debe terminar en fin de línea
                    if (Actual.Codigo != TkSalto && Actual.Codigo != TkEof && !_finDeLineaOmitido)
                        throw ErrorEn(Actual, "Se esperaba fin de línea al terminar la instrucción");
                    _finDeLineaOmitido = false;
                }
                catch (ErrorSintactico)
                {
                    _finDeLineaOmitido = false;
                    Sincronizar();
                }
            }
        }

        private bool EsFinDeBloque()
        {
            return Actual.Codigo is TkEof or TkFinish or TkEnd or TkElse or TkElsif;
        }

        private NodoAST ParseSentencia()
        {
            switch (Actual.Codigo)
            {
                case TkWhole:
                case TkDec:
                    return ParseDeclaracion();
                case TkId:
                    return ParseAsignacion();
                case TkGets:
                    return ParseEntrada();
                case TkPuts:
                    return ParseSalida();
                case TkIf:
                    return ParseCondicional(null);
                case TkWhile:
                    return ParseWhile();
                default:
                    throw ErrorEn(Actual, "Se esperaba una instrucción (declaración, asignación, gets, puts, if o while)");
            }
        }

        // ------------------------------------------------------------------
        //  Instrucciones simples
        // ------------------------------------------------------------------
        private NodoAST ParseDeclaracion()
        {
            Token tipo = Avanzar();
            Token id = Esperar(TkId, "un identificador después del tipo");
            return new NodoDeclaracion(tipo.Codigo == TkWhole ? TipoDato.Whole : TipoDato.Dec, id.Lexema, tipo.Linea);
        }

        private NodoAST ParseAsignacion()
        {
            Token id = Avanzar();
            Esperar(TkAsigna, "'=' después del identificador");
            NodoAST expresion = ParseExpresion();
            return new NodoAsignacion(id.Lexema, expresion, id.Linea);
        }

        private NodoAST ParseEntrada()
        {
            Token gets = Avanzar();
            Token id = Esperar(TkId, "el nombre de una variable después de 'gets'");
            var elementos = new List<NodoAST> { new NodoIdentificador(id.Lexema, id.Linea) };
            return new NodoEntradaSalida("gets", elementos, gets.Linea);
        }

        private NodoAST ParseSalida()
        {
            Token puts = Avanzar();
            var elementos = new List<NodoAST> { ParseElemSalida() };
            while (Actual.Codigo == TkComa)
            {
                Avanzar();
                elementos.Add(ParseElemSalida());
            }
            return new NodoEntradaSalida("puts", elementos, puts.Linea);
        }

        // ELEM_SALIDA -> cadena | EXPRESION
        private NodoAST ParseElemSalida()
        {
            if (!_cadenaComoOperando && Actual.Codigo == TkCadena)
            {
                Token cad = Avanzar();
                return new NodoLiteral(cad.Lexema, TipoDato.Cadena, cad.Linea);
            }
            return ParseExpresion();
        }

        // ------------------------------------------------------------------
        //  Estructuras de control
        // ------------------------------------------------------------------

        // 'if' y cada 'elsif' son un NodoEstructuraControl("if"); el elsif cuelga de Alternativa y el else
        // es un NodoPrograma. Solo el último eslabón de la cadena consume el 'end'.
        private NodoEstructuraControl ParseCondicional(int? lineaIfOriginal)
        {
            Token tokIf = Avanzar();   // 'if' o 'elsif'
            int lineaInicio = lineaIfOriginal ?? tokIf.Linea;

            NodoAST condicion = ParseCondicionEncabezado(tokIf.Linea);
            ExigirSaltoEnEncabezado($"después de la condición del {tokIf.Lexema}");

            var cuerpo = new List<NodoAST>();
            ParseBloque(cuerpo);

            NodoAST? alternativa = null;
            if (Actual.Codigo == TkElsif)
            {
                alternativa = ParseCondicional(lineaInicio);
            }
            else
            {
                if (Actual.Codigo == TkElse)
                {
                    Token tokElse = Avanzar();
                    ExigirSaltoEnEncabezado("después de 'else'");
                    var bloqueElse = new NodoPrograma { Linea = tokElse.Linea };
                    ParseBloque(bloqueElse.Instrucciones);
                    alternativa = bloqueElse;
                }
                ConsumirEnd("if", lineaInicio);
            }

            return new NodoEstructuraControl("if", condicion, cuerpo, tokIf.Linea, alternativa);
        }

        private NodoAST ParseWhile()
        {
            Token tokWhile = Avanzar();
            NodoAST condicion = ParseCondicionEncabezado(tokWhile.Linea);
            ExigirSaltoEnEncabezado("después de la condición del while");

            var cuerpo = new List<NodoAST>();
            ParseBloque(cuerpo);
            ConsumirEnd("while", tokWhile.Linea);

            return new NodoEstructuraControl("while", condicion, cuerpo, tokWhile.Linea);
        }

        // Si la condición tiene un error, se reporta y se descarta el resto de la línea del encabezado,
        // pero el cuerpo y el 'end' se siguen leyendo: así un error en la condición no desordena todo lo demás.
        private NodoAST ParseCondicionEncabezado(int linea)
        {
            try
            {
                return ParseCondicion();
            }
            catch (ErrorSintactico)
            {
                Sincronizar();
                // Condición de relleno solo para que el AST quede completo; con errores sintácticos no se analiza.
                return new NodoCondicion("==", new NodoLiteral("0", TipoDato.Whole, linea), new NodoLiteral("0", TipoDato.Whole, linea), linea);
            }
        }

        private void ConsumirEnd(string estructura, int linea)
        {
            if (Actual.Codigo == TkEnd)
            {
                Avanzar();
                return;
            }
            ReportarError(Actual.Linea, $"Se esperaba 'end' para cerrar el {estructura} de la línea {linea}, pero se encontró {Describir(Actual)}.");
            _finDeLineaOmitido = true;
        }

        // Después de if/elsif/while/else/start debe venir un SALTO.
        private void ExigirSaltoEnEncabezado(string donde)
        {
            if (Actual.Codigo == TkSalto || Actual.Codigo == TkEof || Actual.Codigo == TkFinish) return;
            if (Actual.Codigo < TkPrimerError)
                ReportarError(Actual.Linea, $"Se esperaba fin de línea {donde}, pero se encontró {Describir(Actual)}.");
            Sincronizar();
        }

        // ------------------------------------------------------------------
        //  Condición y expresiones
        // ------------------------------------------------------------------
        private NodoAST ParseCondicion()
        {
            NodoAST izquierdo = ParseExpresion();

            Token op = Actual;
            if (op.Codigo < TkRelPrimero || op.Codigo > TkRelUltimo)
                throw ErrorEn(op, "Se esperaba un operador relacional (==, !=, <, <=, >, >=)");
            Avanzar();

            NodoAST derecho = ParseExpresion();
            return new NodoCondicion(op.Lexema, izquierdo, derecho, op.Linea);
        }

        // EXPRESION -> TERMINO { (+|-) TERMINO }   (asociativa a la izquierda)
        private NodoAST ParseExpresion()
        {
            NodoAST izquierdo = ParseTermino();
            while (Actual.Codigo == TkMas || Actual.Codigo == TkMenos)
            {
                Token op = Avanzar();
                NodoAST derecho = ParseTermino();
                izquierdo = new NodoOperacionBinaria(op.Lexema, izquierdo, derecho, op.Linea);
            }
            return izquierdo;
        }

        // TERMINO -> FACTOR { (*|/) FACTOR }
        private NodoAST ParseTermino()
        {
            NodoAST izquierdo = ParseFactor();
            while (Actual.Codigo == TkPor || Actual.Codigo == TkEntre)
            {
                Token op = Avanzar();
                NodoAST derecho = ParseFactor();
                izquierdo = new NodoOperacionBinaria(op.Lexema, izquierdo, derecho, op.Linea);
            }
            return izquierdo;
        }

        // FACTOR -> ( EXPRESION ) | id | num_entero | num_real
        private NodoAST ParseFactor()
        {
            Token t = Actual;

            if (_cadenaComoOperando && t.Codigo == TkCadena)
            {
                Avanzar();
                return new NodoLiteral(t.Lexema, TipoDato.Cadena, t.Linea);
            }

            switch (t.Codigo)
            {
                case TkId:
                    Avanzar();
                    return new NodoIdentificador(t.Lexema, t.Linea);

                case TkEntero:
                    Avanzar();
                    return new NodoLiteral(t.Lexema, TipoDato.Whole, t.Linea);

                case TkReal:
                    Avanzar();
                    return new NodoLiteral(t.Lexema, TipoDato.Dec, t.Linea);

                case TkParAbre:
                {
                    Avanzar();
                    NodoAST interna = ParseExpresion();
                    Esperar(TkParCierra, "')' para cerrar el paréntesis");
                    return interna;   // los paréntesis no generan nodo: solo fijan la precedencia
                }

                default:
                    throw ErrorEn(t, "Se esperaba un operando (identificador, número o '(')");
            }
        }

        // ------------------------------------------------------------------
        //  Utilidades
        // ------------------------------------------------------------------
        private Token Avanzar()
        {
            Token t = _tokens[_pos];
            if (_pos < _tokens.Count - 1) _pos++;   // nunca pasa del centinela EOF
            return t;
        }

        private Token Esperar(int codigo, string descripcion)
        {
            if (Actual.Codigo == codigo) return Avanzar();
            throw ErrorEn(Actual, $"Se esperaba {descripcion}");
        }

        private void SaltarSaltos()
        {
            while (Actual.Codigo == TkSalto) Avanzar();
        }

        // Modo pánico: descarta tokens hasta el fin de la línea (sin pasarse de finish ni de EOF).
        private void Sincronizar()
        {
            while (Actual.Codigo != TkSalto && Actual.Codigo != TkEof && Actual.Codigo != TkFinish)
                Avanzar();
        }

        private ErrorSintactico ErrorEn(Token t, string mensaje)
        {
            // Los tokens de error léxico ya se reportaron en el scanner: no se duplica el error.
            if (t.Codigo < TkPrimerError)
                ReportarError(t.Linea, $"{mensaje}, pero se encontró {Describir(t)}.");
            return new ErrorSintactico();
        }

        private static string Describir(Token t)
        {
            return t.Codigo switch
            {
                TkEof => "fin de archivo",
                TkSalto => "fin de línea",
                _ => $"'{t.Lexema}'"
            };
        }

        private void ReportarError(int linea, string mensaje)
        {
            _errores.Add($"[Línea {linea,2}] Error Sintáctico: {mensaje}");
        }

        public void ImprimirErrores()
        {
            Console.WriteLine("================ REPORTE DE ERRORES SINTÁCTICOS ================");
            if (_errores.Count == 0)
            {
                Console.WriteLine("No se encontraron errores sintácticos.");
            }
            else
            {
                foreach (var err in _errores)
                {
                    Console.WriteLine(err);
                }
            }

            Console.WriteLine($"\nTotal de errores sintácticos detectados: {_errores.Count}");
        }
    }
}
