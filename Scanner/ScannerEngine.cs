using System.Collections.Generic;
using NanoRBCompiler.Errores;

namespace NanoRBCompiler.Scanner
{
    public class ScannerEngine
    {
        private readonly string _codigoFuente;
        private int _posicion = 0;
        private int _lineaActual = 1;
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

            while (_posicion < _codigoFuente.Length)
            {
                char c = _codigoFuente[_posicion];
                ColumnaMatriz col = Clasificador.ObtenerColumna(c);
                string sigEstado = MatrizTransiciones.ObtenerSiguienteEstado(estadoActual, col);

                if (sigEstado == "IGNORAR")
                {
                    _posicion++;
                    continue;
                }

                if (sigEstado == "F_FIN_COMENTARIO")
                {
                    _posicion++;
                    _lineaActual++;
                    estadoActual = "q0";
                    lexema = "";
                    continue;
                }

                if (sigEstado.StartsWith("F") || sigEstado.StartsWith("ERROR"))
                {
                    return ResolverEstadoFinal(sigEstado, lexema, c);
                }

                if (c == '\n') _lineaActual++;
                lexema += c;
                estadoActual = sigEstado;
                _posicion++;
            }

            return new Token(0, "EOF", _lineaActual);
        }

        private Token ResolverEstadoFinal(string estadoFinal, string lexema, char cActual)
        {
            switch (estadoFinal)
            {
                case "F100":
                    if (MatrizTransiciones.PalabrasReservadas.TryGetValue(lexema, out int codRes))
                        return new Token(codRes, lexema, _lineaActual);
                    return new Token(100, lexema, _lineaActual);

                case "F101": return new Token(101, lexema, _lineaActual);
                case "F102": return new Token(102, lexema, _lineaActual);
                case "F103": _posicion++; return new Token(103, lexema + cActual, _lineaActual);
                case "F104": return new Token(104, lexema, _lineaActual);
                case "F105": _posicion++; return new Token(105, lexema + cActual, _lineaActual);
                case "F106": _posicion++; return new Token(106, lexema + cActual, _lineaActual);
                case "F107": return new Token(107, lexema, _lineaActual);
                case "F108": _posicion++; return new Token(108, lexema + cActual, _lineaActual);
                case "F109": return new Token(109, lexema, _lineaActual);
                case "F110": _posicion++; return new Token(110, lexema + cActual, _lineaActual);

                case "F_OP_ARITMETICO":
                    _posicion++;
                    int codOp = cActual switch { '+' => 111, '-' => 112, '*' => 113, '/' => 114, _ => 111 };
                    return new Token(codOp, cActual.ToString(), _lineaActual);

                case "F_DELIMITADOR":
                    _posicion++;
                    int codDel = cActual switch { '(' => 115, ')' => 116, ',' => 117, _ => 115 };
                    return new Token(codDel, cActual.ToString(), _lineaActual);

                case "F118":
                    _posicion++;
                    _lineaActual++;
                    return new Token(118, "\\n", _lineaActual - 1);

                case "ERROR_501":
                    _posicion++;
                    _manejadorErrores.ReportarError(501, $"Número real mal formado alrededor de '{lexema}'", _lineaActual, lexema);
                    return new Token(501, lexema, _lineaActual);

                case "ERROR_503":
                    _posicion++;
                    _lineaActual++;
                    _manejadorErrores.ReportarError(503, "Cadena de texto sin cerrar antes del salto de línea", _lineaActual - 1, lexema);
                    return new Token(503, lexema, _lineaActual - 1);

                default: // ERROR_500 u otros
                    _posicion++;
                    _manejadorErrores.ReportarError(500, $"Carácter no reconocido o secuencia inválida '{cActual}'", _lineaActual, cActual.ToString());
                    return new Token(500, cActual.ToString(), _lineaActual);
            }
        }
    }
}