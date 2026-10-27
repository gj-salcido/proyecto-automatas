using System.Collections.Generic;
using NanoRBCompiler.Errores;

namespace NanoRBCompiler.Scanner
{
    public class ScannerEngine
    {
        private readonly string _codigoFuente;
        private int _posicion = 0;
        private int _lineaActual = 1;
        private int _columnaActual = 1;
        private readonly ManejadorErrores _manejadorErrores;

        public ScannerEngine(string codigoFuente, ManejadorErrores manejadorErrores)
        {
            _codigoFuente = codigoFuente ?? string.Empty;
            _manejadorErrores = manejadorErrores;
        }

        public List<Token> EscanearTodo()
        {
            var listaTokens = new List<Token>();
            while (_posicion < _codigoFuente.Length)
            {
                Token t = ObtenerSiguienteToken();
                if (t != null && t.Codigo != 0)
                {
                    listaTokens.Add(t);
                }
            }
            return listaTokens;
        }

        public Token ObtenerSiguienteToken()
        {
            string estadoActual = "q0";
            string lexema = "";
            int columnaInicio = _columnaActual;

            while (_posicion < _codigoFuente.Length)
            {
                char c = _codigoFuente[_posicion];
                ColumnaMatriz col = Clasificador.ObtenerColumna(c);
                string sigEstado = MatrizTransiciones.ObtenerSiguienteEstado(estadoActual, col);

                if (sigEstado == "IGNORAR")
                {
                    AvanzarPosicion(c);
                    columnaInicio = _columnaActual;
                    continue;
                }

                if (sigEstado == "F_FIN_COMENTARIO")
                {
                    // En NanoRB, el \n del comentario debe contarse pero NO consumirse aquí
                    // para que el ciclo principal lo reconozca como Token 118 (SALTO)
                    estadoActual = "q0";
                    lexema = "";
                    continue;
                }

                if (sigEstado.StartsWith("F") || sigEstado.StartsWith("ERROR") || sigEstado == "q99")
                {
                    return ResolverEstadoFinal(sigEstado, lexema, c, columnaInicio);
                }

                lexema += c;
                estadoActual = sigEstado;
                AvanzarPosicion(c);
            }

            // CORRECCIÓN 1: al llegar al fin del archivo, el estado final depende de dónde quedó el AFD
            // (antes cualquier lexema pendiente se devolvía como F100, incluso números y comentarios).
            if (!string.IsNullOrEmpty(lexema))
            {
                string? estadoFinalEOF = EstadoFinalAlLlegarAEOF(estadoActual);
                if (estadoFinalEOF != null)
                {
                    return ResolverEstadoFinal(estadoFinalEOF, lexema, '\0', columnaInicio);
                }
            }

            return new Token(0, "EOF", _lineaActual, _columnaActual);
        }

        private static string? EstadoFinalAlLlegarAEOF(string estado)
        {
            return estado switch
            {
                "q1" => "F100",
                "q2" => "F101",
                "q3" => "ERROR_501",
                "q4" => "F102",
                "q5" => "F104",
                "q6" => "ERROR_500",
                "q7" => "F107",
                "q8" => "F109",
                "q22" => "ERROR_503",
                _ => null   // q21 (comentario) y los demás no producen token
            };
        }

        private void AvanzarPosicion(char c)
        {
            _posicion++;
            if (c == '\n')
            {
                _lineaActual++;
                _columnaActual = 1;
            }
            else
            {
                _columnaActual++;
            }
        }

        private Token ResolverEstadoFinal(string estadoFinal, string lexema, char cActual, int colInicio)
        {
            switch (estadoFinal)
            {
                // Estados con Retract (se devuelve el carácter sobrante a la entrada)
                case "F100":
                    if (MatrizTransiciones.PalabrasReservadas.TryGetValue(lexema, out int codRes))
                        return new Token(codRes, lexema, _lineaActual, colInicio);
                    return new Token(100, lexema, _lineaActual, colInicio);

                case "F101": return new Token(101, lexema, _lineaActual, colInicio);
                case "F102": return new Token(102, lexema, _lineaActual, colInicio);
                case "F104": return new Token(104, lexema, _lineaActual, colInicio);
                case "F107": return new Token(107, lexema, _lineaActual, colInicio);
                case "F109": return new Token(109, lexema, _lineaActual, colInicio);

                // Estados de Aceptación de 2 Caracteres (Consumen cActual)
                case "F103":
                    lexema += cActual;
                    AvanzarPosicion(cActual);
                    return new Token(103, lexema, _lineaActual, colInicio);

                case "F105":
                case "F106":
                case "F108":
                case "F110":
                    lexema += cActual;
                    AvanzarPosicion(cActual);
                    int codRel = estadoFinal switch { "F105" => 105, "F106" => 106, "F108" => 108, _ => 110 };
                    return new Token(codRel, lexema, _lineaActual, colInicio);

                // Símbolos de 1 Carácter
                case "F_OP_ARITMETICO":
                    lexema += cActual;
                    AvanzarPosicion(cActual);
                    int codOp = cActual switch { '+' => 111, '-' => 112, '*' => 113, '/' => 114, _ => 111 };
                    return new Token(codOp, lexema, _lineaActual, colInicio);

                case "F_DELIMITADOR":
                    lexema += cActual;
                    AvanzarPosicion(cActual);
                    int codDel = cActual switch { '(' => 115, ')' => 116, ',' => 117, _ => 115 };
                    return new Token(codDel, lexema, _lineaActual, colInicio);

                case "F118":
                    AvanzarPosicion(cActual);
                    return new Token(118, "\\n", _lineaActual - 1, colInicio);

                // Reportes de Error Léxico (Recuperación)
                case "ERROR_501":
                    _manejadorErrores.ReportarError(501, $"Número real mal formado '{lexema}'", _lineaActual, lexema);
                    return new Token(501, lexema, _lineaActual, colInicio);

                // CORRECCIÓN 2: '!' sin '=' -> el error es del '!' (lexema); el carácter siguiente
                // NO se consume, se reinicia desde q0 con ese mismo carácter (como indica la matriz).
                case "ERROR_500":
                    _manejadorErrores.ReportarError(500, $"Secuencia inválida '{lexema}'; se esperaba '='", _lineaActual, lexema);
                    return new Token(500, lexema, _lineaActual, colInicio);

                // CORRECCIÓN 3: cadena sin cerrar -> NO se consume el '\n'; así q0 lo reconoce como
                // token SALTO (118) y el parser no pierde el fin de esa línea.
                case "ERROR_503":
                    _manejadorErrores.ReportarError(503, "Cadena sin cerrar en la misma línea", _lineaActual, lexema);
                    return new Token(503, lexema, _lineaActual, colInicio);

                default: // ERROR_500 o q99 (Carácter ilegal)
                    AvanzarPosicion(cActual);
                    string charErr = cActual.ToString();
                    _manejadorErrores.ReportarError(500, $"Carácter ilegal '{charErr}'", _lineaActual, charErr);
                    return new Token(500, charErr, _lineaActual, colInicio);
            }
        }
    }
}