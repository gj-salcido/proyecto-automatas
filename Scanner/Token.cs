namespace NanoRBCompiler.Scanner
{
    public class Token
    {
        public int Codigo { get; }
        public string Lexema { get; }
        public int Linea { get; }
        public int Columna { get; }

        public Token(int codigo, string lexema, int linea, int columna = 1)
        {
            Codigo = codigo;
            Lexema = lexema ?? string.Empty;
            Linea = linea;
            Columna = columna;
        }

        public override string ToString()
        {
            return $"⟨Token: {Codigo,-3} | Lexema: \"{Lexema}\" | Posición: L{Linea}:C{Columna}⟩";
        }
    }
}