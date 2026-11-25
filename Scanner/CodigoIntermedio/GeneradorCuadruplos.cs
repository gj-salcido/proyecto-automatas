using System;
using System.Collections.Generic;
using System.Globalization;
using NanoRBCompiler.Semantico;
using NanoRBCompiler.TablaSimbolos;

namespace NanoRBCompiler.CodigoIntermedio
{
    /// <summary>
    /// Fase 4: recorre el AST ya tipado y emite los cuádruplos del esquema de traducción (Ejercicio 4).
    ///
    /// Entrada garantizada: programa sin errores léxicos, sintácticos ni semánticos. El generador no revisa nada
    /// de eso: lo único que comprueba es que el semántico no haya encontrado errores, y ante un nodo que no
    /// esperaba lanza InvalidOperationException (sería un bug del compilador, no un error del usuario).
    ///
    /// El recorrido es en postorden para las expresiones (hijos primero, luego el operador). Las estructuras de
    /// control son la excepción: en el while la etiqueta de inicio va ANTES de los cuádruplos de la condición,
    /// para que cada vuelta del ciclo vuelva a evaluarla.
    ///
    /// Dónde se convierte entero -> real (int2float), según el TipoResultado que dejó el semántico:
    ///   - operación: el operando Whole cuando el resultado de la operación es Dec
    ///   - asignación: la expresión Whole cuando la variable destino es Dec
    ///   - condición: el operando Whole cuando el otro es Dec
    /// </summary>
    public class GeneradorCuadruplos
    {
        private readonly TablaSimbolosManager _simbolos;
        private readonly TablaTemporales _temporales;
        private readonly TablaCadenas _cadenas;
        private readonly List<Cuadruplo> _cuadruplos = new();
        private readonly Dictionary<string, int> _etiquetas = new();
        private int _contadorEtiquetas = 0;
        private bool _generado = false;

        public GeneradorCuadruplos(AnalizadorSemantico semantico)
        {
            ArgumentNullException.ThrowIfNull(semantico);
            if (semantico.TieneErrores)
                throw new InvalidOperationException("La Fase 4 no se ejecuta si el análisis semántico encontró errores.");

            _simbolos = semantico.Tabla;
            _temporales = new TablaTemporales(_simbolos);
            _cadenas = new TablaCadenas(_simbolos);
        }

        /// <summary>Genera el código intermedio. Se usa una sola vez por generador.</summary>
        public ResultadoFase4 Generar(NodoPrograma programa)
        {
            ArgumentNullException.ThrowIfNull(programa);
            if (_generado)
                throw new InvalidOperationException("Este generador ya produjo su resultado; crea uno nuevo para otro programa.");
            _generado = true;

            GenerarSentencia(programa);
            return new ResultadoFase4(_cuadruplos, _temporales, _cadenas, _etiquetas, _simbolos);
        }

        // ------------------------------------------------------------------
        //  Sentencias
        // ------------------------------------------------------------------
        private void GenerarSentencia(NodoAST nodo)
        {
            switch (nodo)
            {
                case NodoPrograma bloque:
                    foreach (var inst in bloque.Instrucciones) GenerarSentencia(inst);
                    break;

                case NodoDeclaracion:
                    break;   // no genera código: solo registra el identificador y su tipo en la tabla

                case NodoAsignacion asig:
                    GenerarAsignacion(asig);
                    break;

                case NodoEntradaSalida es:
                    if (es.Instruccion == "gets") GenerarEntrada(es);
                    else if (es.Instruccion == "puts") GenerarSalida(es);
                    else throw new InvalidOperationException($"Instrucción de entrada/salida desconocida: '{es.Instruccion}'.");
                    break;

                case NodoEstructuraControl ctrl:
                    if (ctrl.TipoEstructura == "if") GenerarIf(ctrl);
                    else if (ctrl.TipoEstructura == "while") GenerarWhile(ctrl);
                    else throw new InvalidOperationException($"Estructura de control desconocida: '{ctrl.TipoEstructura}'.");
                    break;

                default:
                    throw new InvalidOperationException($"Nodo de sentencia inesperado: {nodo.GetType().Name}.");
            }
        }

        // ASIGNACION -> id = EXPRESION :  gen('=', EXPRESION.lugar, '_', id.nombre)
        private void GenerarAsignacion(NodoAsignacion asig)
        {
            string valor = GenerarExpresion(asig.Expresion);

            if (!_simbolos.TryObtener(asig.Variable, out Simbolo? simbolo) || simbolo == null)
                throw new InvalidOperationException($"La variable '{asig.Variable}' no está en la tabla de símbolos.");

            valor = Convertir(valor, asig.Expresion.TipoResultado, simbolo.Tipo);
            Emitir("=", valor, Cuadruplo.Vacio, asig.Variable);
        }

        // ENTRADA -> gets id :  gen('read', '_', '_', id.nombre)
        private void GenerarEntrada(NodoEntradaSalida es)
        {
            foreach (var elemento in es.Elementos)
            {
                if (elemento is not NodoIdentificador id)
                    throw new InvalidOperationException("gets debe recibir identificadores.");
                Emitir("read", Cuadruplo.Vacio, Cuadruplo.Vacio, id.Nombre);
            }
        }

        // SALIDA -> puts LISTA_SALIDA :  un print por elemento y, al final, un print del salto de línea
        private void GenerarSalida(NodoEntradaSalida es)
        {
            foreach (var elemento in es.Elementos)
            {
                string lugar = GenerarExpresion(elemento);
                Emitir("print", lugar, Cuadruplo.Vacio, Cuadruplo.Vacio);
            }

            // puts termina con salto de línea
            Emitir("print", _cadenas.Obtener("\n").Nombre, Cuadruplo.Vacio, Cuadruplo.Vacio);
        }

        // ------------------------------------------------------------------
        //  Estructuras de control
        // ------------------------------------------------------------------

        // CONDICIONAL -> if ... { elsif ... } [ else ... ] end
        //   L_fin = nuevaEtiqueta(); por cada rama (if y cada elsif): L_sig = nuevaEtiqueta()
        //   cond; if_false cond, L_sig; cuerpo; goto L_fin; label L_sig
        //   el else es solo su cuerpo; al final: label L_fin
        private void GenerarIf(NodoEstructuraControl ctrl)
        {
            string lFin = NuevaEtiqueta();
            NodoEstructuraControl rama = ctrl;

            while (true)
            {
                string lSig = NuevaEtiqueta();

                string cond = GenerarCondicion(rama.Condicion);
                Emitir("if_false", cond, lSig, Cuadruplo.Vacio);
                foreach (var inst in rama.Cuerpo) GenerarSentencia(inst);
                Emitir("goto", lFin, Cuadruplo.Vacio, Cuadruplo.Vacio);
                PonerEtiqueta(lSig);

                if (rama.Alternativa is NodoEstructuraControl elsif)
                {
                    rama = elsif;   // el siguiente elsif de la cadena
                    continue;
                }

                if (rama.Alternativa is NodoPrograma bloqueElse)
                {
                    GenerarSentencia(bloqueElse);
                }
                break;
            }

            PonerEtiqueta(lFin);
        }

        // CICLO_WHILE -> while CONDICION SALTO LINEAS end
        //   L_inicio, L_fin; label L_inicio; cond; if_false cond, L_fin; cuerpo; goto L_inicio; label L_fin
        private void GenerarWhile(NodoEstructuraControl ctrl)
        {
            string lInicio = NuevaEtiqueta();
            string lFin = NuevaEtiqueta();

            PonerEtiqueta(lInicio);
            string cond = GenerarCondicion(ctrl.Condicion);
            Emitir("if_false", cond, lFin, Cuadruplo.Vacio);
            foreach (var inst in ctrl.Cuerpo) GenerarSentencia(inst);
            Emitir("goto", lInicio, Cuadruplo.Vacio, Cuadruplo.Vacio);
            PonerEtiqueta(lFin);
        }

        // CONDICION -> EXPRESION OP_REL EXPRESION :  t = nuevoTemp(); gen(OP_REL.lexema, e1.lugar, e2.lugar, t)
        private string GenerarCondicion(NodoAST nodo)
        {
            if (nodo is not NodoCondicion cond)
                throw new InvalidOperationException($"Se esperaba una condición y llegó {nodo.GetType().Name}.");

            string izq = GenerarExpresion(cond.Izquierdo);
            string der = GenerarExpresion(cond.Derecho);

            // Comparación mixta: se compara en real, así que el entero se convierte
            TipoDato tIzq = cond.Izquierdo.TipoResultado;
            TipoDato tDer = cond.Derecho.TipoResultado;
            TipoDato comun = (tIzq == TipoDato.Dec || tDer == TipoDato.Dec) ? TipoDato.Dec : TipoDato.Whole;
            izq = Convertir(izq, tIzq, comun);
            der = Convertir(der, tDer, comun);

            // El resultado lógico se guarda como entero (igual que lo tipa el analizador semántico)
            Temporal t = _temporales.Nuevo(TipoDato.Whole);
            Emitir(cond.OperadorRelacional, izq, der, t.Nombre);
            return t.Nombre;
        }

        // ------------------------------------------------------------------
        //  Expresiones: devuelven el "lugar" donde queda el valor (nombre, literal o temporal)
        // ------------------------------------------------------------------
        private string GenerarExpresion(NodoAST nodo)
        {
            switch (nodo)
            {
                case NodoLiteral lit:
                    return LugarDeLiteral(lit);

                case NodoIdentificador id:
                    return id.Nombre;

                case NodoOperacionBinaria op:
                {
                    string izq = GenerarExpresion(op.Izquierdo);
                    string der = GenerarExpresion(op.Derecho);

                    // Promoción: si la operación es real, el operando entero se convierte
                    izq = Convertir(izq, op.Izquierdo.TipoResultado, op.TipoResultado);
                    der = Convertir(der, op.Derecho.TipoResultado, op.TipoResultado);

                    Temporal t = _temporales.Nuevo(op.TipoResultado);
                    Emitir(op.Operador, izq, der, t.Nombre);
                    return t.Nombre;
                }

                default:
                    throw new InvalidOperationException($"Nodo de expresión inesperado: {nodo.GetType().Name}.");
            }
        }

        private string LugarDeLiteral(NodoLiteral lit)
        {
            switch (lit.TipoLiteral)
            {
                case TipoDato.Whole:
                    return long.Parse(lit.Valor, NumberStyles.None, CultureInfo.InvariantCulture)
                               .ToString(CultureInfo.InvariantCulture);

                case TipoDato.Dec:
                {
                    // dec guarda un solo decimal: se trunca igual que lo anunció la advertencia del semántico
                    decimal d = decimal.Parse(lit.Valor, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                    d = decimal.Truncate(d * 10m) / 10m;
                    return d.ToString("0.0", CultureInfo.InvariantCulture);
                }

                case TipoDato.Cadena:
                {
                    string texto = lit.Valor;
                    if (texto.Length >= 2 && texto[0] == '"' && texto[texto.Length - 1] == '"')
                        texto = texto.Substring(1, texto.Length - 2);
                    return _cadenas.Obtener(texto).Nombre;
                }

                default:
                    throw new InvalidOperationException($"Literal de tipo inesperado: {lit.TipoLiteral}.");
            }
        }

        // int2float solo en un sentido: entero -> real (real -> entero no está permitido por el semántico)
        private string Convertir(string lugar, TipoDato origen, TipoDato destino)
        {
            if (origen == TipoDato.Whole && destino == TipoDato.Dec)
            {
                Temporal t = _temporales.Nuevo(TipoDato.Dec);
                Emitir("int2float", lugar, Cuadruplo.Vacio, t.Nombre);
                return t.Nombre;
            }
            return lugar;
        }

        // ------------------------------------------------------------------
        //  Emisión
        // ------------------------------------------------------------------
        private void Emitir(string op, string arg1, string arg2, string resultado)
        {
            _cuadruplos.Add(new Cuadruplo(_cuadruplos.Count + 1, op, arg1, arg2, resultado));
        }

        // L1, L2... ("L1" es un identificador válido: se salta si el usuario ya lo usa)
        private string NuevaEtiqueta()
        {
            string nombre;
            do
            {
                _contadorEtiquetas++;
                nombre = "L" + _contadorEtiquetas;
            }
            while (_simbolos.TryObtener(nombre, out _));
            return nombre;
        }

        // gen('label', L, '_', '_') y registro del número del cuádruplo donde queda definida
        private void PonerEtiqueta(string etiqueta)
        {
            Emitir("label", etiqueta, Cuadruplo.Vacio, Cuadruplo.Vacio);
            _etiquetas[etiqueta] = _cuadruplos.Count;
        }
    }
}
