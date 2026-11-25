namespace NanoRBCompiler.Scanner
{
    public enum ColumnaMatriz
    {
        Letra = 0,
        Digito = 1,
        Punto = 2,
        Comilla = 3,
        Igual = 4,
        Admiracion = 5,
        Menor = 6,
        Mayor = 7,
        OpAritmetico = 8,
        Delimitador = 9,
        Salto = 10,
        Comentario = 11,
        EspacioTab = 12,
        Otro = 13
    }

    public static class Clasificador
    {
        public static ColumnaMatriz ObtenerColumna(char c)
        {
            // La especificación pide [a-zA-Z] y [0-9]; char.IsLetter / char.IsDigit aceptarían ñ, á, dígitos árabes, etc.
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return ColumnaMatriz.Letra;
            if (c >= '0' && c <= '9') return ColumnaMatriz.Digito;

            return c switch
            {
                '.' => ColumnaMatriz.Punto,
                '"' => ColumnaMatriz.Comilla,
                '=' => ColumnaMatriz.Igual,
                '!' => ColumnaMatriz.Admiracion,
                '<' => ColumnaMatriz.Menor,
                '>' => ColumnaMatriz.Mayor,
                '+' or '-' or '*' or '/' => ColumnaMatriz.OpAritmetico,
                '(' or ')' or ',' => ColumnaMatriz.Delimitador,
                '\n' => ColumnaMatriz.Salto,
                '#' => ColumnaMatriz.Comentario,
                ' ' or '\t' or '\r' => ColumnaMatriz.EspacioTab,
                _ => ColumnaMatriz.Otro
            };
        }
    }
}