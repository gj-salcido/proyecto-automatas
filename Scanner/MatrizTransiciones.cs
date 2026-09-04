using System.Collections.Generic;

namespace NanoRBCompiler.Scanner
{
    public static class MatrizTransiciones
    {
        // Palabras Reservadas según el Ejercicio 1 (200..210)
        public static readonly Dictionary<string, int> PalabrasReservadas = new()
        {
            { "start", 200 }, { "finish", 201 }, { "whole", 202 },
            { "dec", 203 },   { "if", 204 },     { "elsif", 205 },
            { "else", 206 },  { "end", 207 },    { "while", 208 },
            { "gets", 209 },  { "puts", 210 }
        };

        public static string ObtenerSiguienteEstado(string estadoActual, ColumnaMatriz columna)
        {
            return (estadoActual, columna) switch
            {
                // q0 (Estado Inicial)
                ("q0", ColumnaMatriz.Letra) => "q1",
                ("q0", ColumnaMatriz.Digito) => "q2",
                ("q0", ColumnaMatriz.Punto) => "q99",
                ("q0", ColumnaMatriz.Comilla) => "q22",
                ("q0", ColumnaMatriz.Igual) => "q5",
                ("q0", ColumnaMatriz.Admiracion) => "q6",
                ("q0", ColumnaMatriz.Menor) => "q7",
                ("q0", ColumnaMatriz.Mayor) => "q8",
                ("q0", ColumnaMatriz.OpAritmetico) => "F_OP_ARITMETICO",
                ("q0", ColumnaMatriz.Delimitador) => "F_DELIMITADOR",
                ("q0", ColumnaMatriz.Salto) => "F118",
                ("q0", ColumnaMatriz.Comentario) => "q21",
                ("q0", ColumnaMatriz.EspacioTab) => "IGNORAR",

                // q1 (Identificador / Palabra Reservada)
                ("q1", ColumnaMatriz.Letra or ColumnaMatriz.Digito) => "q1",
                ("q1", _) => "F100",

                // q2 (Entero)
                ("q2", ColumnaMatriz.Digito) => "q2",
                ("q2", ColumnaMatriz.Punto) => "q3",
                ("q2", _) => "F101",

                // q3 (Punto decimal)
                ("q3", ColumnaMatriz.Digito) => "q4",
                ("q3", _) => "ERROR_501",

                // q4 (Real)
                ("q4", ColumnaMatriz.Digito) => "q4",
                ("q4", _) => "F102",

                // q5 (Evaluación =)
                ("q5", ColumnaMatriz.Igual) => "F105",
                ("q5", _) => "F104",

                // q6 (Evaluación !)
                ("q6", ColumnaMatriz.Igual) => "F106",
                ("q6", _) => "ERROR_500",

                // q7 (Evaluación <)
                ("q7", ColumnaMatriz.Igual) => "F108",
                ("q7", _) => "F107",

                // q8 (Evaluación >)
                ("q8", ColumnaMatriz.Igual) => "F110",
                ("q8", _) => "F109",

                // q21 (Comentario)
                ("q21", ColumnaMatriz.Salto) => "F_FIN_COMENTARIO",
                ("q21", _) => "q21",

                // q22 (Cadena entre comillas)
                ("q22", ColumnaMatriz.Comilla) => "F103",
                ("q22", ColumnaMatriz.Salto) => "ERROR_503",
                ("q22", _) => "q22",

                _ => "q99"
            };
        }
    }
}