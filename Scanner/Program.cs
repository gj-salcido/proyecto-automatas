using System;
using System.Collections.Generic;
using NanoRBCompiler.Errores;
using NanoRBCompiler.CodigoIntermedio;
using NanoRBCompiler.Parser;
using NanoRBCompiler.Scanner;
using NanoRBCompiler.Semantico;
using NanoRBCompiler.TablaSimbolos;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;   // acentos y ⟨ ⟩ en la consola de Windows
        Console.WriteLine("================ COMPILADOR NANORB - FASES 1 A 4 + PREPARACIÓN FASE 6 ================\n");

        ProgramaEjercicio5();
        Console.WriteLine();
        ProgramaControlYEntradaSalida();
        Console.WriteLine();
        ProgramaBaseIntegrado();
        Console.WriteLine();
        Console.WriteLine("================ PIPELINE COMPLETO DESDE TEXTO: SCANNER -> PARSER -> SEMÁNTICO -> CÓDIGO INTERMEDIO ================\n");
        PipelineCompleto();
    }

    // Atajos para escribir el AST (hasta que el parser de la Fase 2 lo construya solo)
    static NodoLiteral Ent(string v, int l) => new NodoLiteral(v, TipoDato.Whole, l);
    static NodoLiteral Real(string v, int l) => new NodoLiteral(v, TipoDato.Dec, l);
    static NodoLiteral Cad(string v, int l) => new NodoLiteral(v, TipoDato.Cadena, l);
    static NodoIdentificador Id(string n, int l) => new NodoIdentificador(n, l);
    static NodoPrograma Bloque(params NodoAST[] instrucciones)
    {
        var b = new NodoPrograma();
        b.Instrucciones.AddRange(instrucciones);
        return b;
    }

    // ------------------------------------------------------------------
    //  Programa del Ejercicio 5 (el mismo texto, con estas líneas)
    //
    //   1  start
    //   2    # Declaración de variables
    //   3    whole contador
    //   4    dec total
    //   5    whole contador                 # Error 1 (Declaración doble)
    //   6    contador = 1
    //   7    total = 0.5
    //   8
    //   9    desconocida = 10               # Error 2 (Variable no declarada)
    //  10    total = desconocida + 1        # Prueba anti-cascada
    //  11
    //  12    while contador <= 5
    //  13      total = total + 1.5
    //  14      contador = contador + 1
    //  15    end
    //  16    total = "hola"                 # Error 3 (Tipos incompatibles)
    //  17    puts total
    //  18  finish
    // ------------------------------------------------------------------
    static void ProgramaEjercicio5()
    {
        Console.WriteLine("--- Programa del Ejercicio 5 ---");
        var semantico = new AnalizadorSemantico();
        var programa = new NodoPrograma();

        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Whole, "contador", 3));
        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Dec, "total", 4));
        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Whole, "contador", 5));   // ERROR 1
        programa.Instrucciones.Add(new NodoAsignacion("contador", Ent("1", 6), 6));
        programa.Instrucciones.Add(new NodoAsignacion("total", Real("0.5", 7), 7));

        programa.Instrucciones.Add(new NodoAsignacion("desconocida", Ent("10", 9), 9));   // ERROR 2

        // Anti-cascada: 'desconocida' ya es errónea, pero la suma y la asignación no agregan más errores
        programa.Instrucciones.Add(new NodoAsignacion(
            "total",
            new NodoOperacionBinaria("+", Id("desconocida", 10), Ent("1", 10), 10),
            10));

        programa.Instrucciones.Add(new NodoEstructuraControl(
            "while",
            new NodoCondicion("<=", Id("contador", 12), Ent("5", 12), 12),
            new List<NodoAST>
            {
                new NodoAsignacion("total", new NodoOperacionBinaria("+", Id("total", 13), Real("1.5", 13), 13), 13),
                new NodoAsignacion("contador", new NodoOperacionBinaria("+", Id("contador", 14), Ent("1", 14), 14), 14)
            },
            12));

        programa.Instrucciones.Add(new NodoAsignacion("total", Cad("\"hola\"", 16), 16)); // ERROR 3
        programa.Instrucciones.Add(new NodoEntradaSalida("puts", new List<NodoAST> { Id("total", 17) }, 17));

        semantico.ComprobarTipos(programa);
        semantico.ImprimirErrores();
    }

    // ------------------------------------------------------------------
    //  Cubre if / elsif / else, gets y puts con lista
    //
    //   1  start
    //   2    whole x
    //   3    dec total
    //   4    gets x
    //   5    gets y                         # y no está declarada (1 solo error)
    //   6    if x == 5
    //   7      puts "Mitad: ", x
    //   8    elsif x != 2
    //   9      total = total + 3
    //  10    else
    //  11      puts "fin"
    //  12    end
    //  13  finish
    // ------------------------------------------------------------------
    static void ProgramaControlYEntradaSalida()
    {
        Console.WriteLine("--- if / elsif / else, gets y puts con lista ---");
        var semantico = new AnalizadorSemantico();
        var programa = new NodoPrograma();

        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Whole, "x", 2));
        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Dec, "total", 3));
        programa.Instrucciones.Add(new NodoEntradaSalida("gets", new List<NodoAST> { Id("x", 4) }, 4));
        programa.Instrucciones.Add(new NodoEntradaSalida("gets", new List<NodoAST> { Id("y", 5) }, 5));   // ERROR

        var bloqueElse = Bloque(
            new NodoEntradaSalida("puts", new List<NodoAST> { Cad("\"fin\"", 11) }, 11));

        var ramaElsif = new NodoEstructuraControl(
            "if",
            new NodoCondicion("!=", Id("x", 8), Ent("2", 8), 8),
            new List<NodoAST>
            {
                new NodoAsignacion("total", new NodoOperacionBinaria("+", Id("total", 9), Ent("3", 9), 9), 9)
            },
            8,
            bloqueElse);

        programa.Instrucciones.Add(new NodoEstructuraControl(
            "if",
            new NodoCondicion("==", Id("x", 6), Ent("5", 6), 6),
            new List<NodoAST>
            {
                new NodoEntradaSalida("puts", new List<NodoAST> { Cad("\"Mitad: \"", 7), Id("x", 7) }, 7)
            },
            6,
            ramaElsif));

        semantico.ComprobarTipos(programa);
        semantico.ImprimirErrores();
    }

    // ------------------------------------------------------------------
    //  Programa base (el de los cuádruplos de la Fase 6), con el pipeline integrado:
    //  scanner -> tabla de símbolos compartida -> semántico -> offsets para el .asm
    //
    //   1  start
    //   2    # Declaración de variables
    //   3    whole contador
    //   4    dec total
    //   5
    //   6    contador = 1
    //   7    total = 0.5
    //   8
    //   9    while contador <= 5
    //  10      total = total + 1.5
    //  11      contador = contador + 1
    //  12    end
    //  13
    //  14    total = 10                     # whole -> dec: el generador insertará int2float
    //  15    puts total
    //  16  finish
    // ------------------------------------------------------------------
    static void ProgramaBaseIntegrado()
    {
        Console.WriteLine("--- Programa base: scanner + semántico + offsets (Fase 6) ---");

        string fuente = string.Join("\n", new[]
        {
            "start",
            "  # Declaración de variables",
            "  whole contador",
            "  dec total",
            "",
            "  contador = 1",
            "  total = 0.5",
            "",
            "  while contador <= 5",
            "    total = total + 1.5",
            "    contador = contador + 1",
            "  end",
            "",
            "  total = 10",
            "  puts total",
            "finish"
        }) + "\n";

        // 1) Léxico: llena la tabla con cada identificador y sus líneas
        var tabla = new TablaSimbolosManager();
        var erroresLexicos = new ManejadorErrores();
        var tokens = new ScannerEngine(fuente, erroresLexicos, tabla).EscanearTodo();
        Console.WriteLine($"Tokens reconocidos: {tokens.Count}");
        erroresLexicos.ImprimirErrores();

        // 2) Semántico: usa LA MISMA tabla y le agrega los tipos
        var semantico = new AnalizadorSemantico(tabla);
        var programa = new NodoPrograma();

        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Whole, "contador", 3));
        programa.Instrucciones.Add(new NodoDeclaracion(TipoDato.Dec, "total", 4));
        programa.Instrucciones.Add(new NodoAsignacion("contador", Ent("1", 6), 6));
        programa.Instrucciones.Add(new NodoAsignacion("total", Real("0.5", 7), 7));
        programa.Instrucciones.Add(new NodoEstructuraControl(
            "while",
            new NodoCondicion("<=", Id("contador", 9), Ent("5", 9), 9),
            new List<NodoAST>
            {
                new NodoAsignacion("total", new NodoOperacionBinaria("+", Id("total", 10), Real("1.5", 10), 10), 10),
                new NodoAsignacion("contador", new NodoOperacionBinaria("+", Id("contador", 11), Ent("1", 11), 11), 11)
            },
            9));
        programa.Instrucciones.Add(new NodoAsignacion("total", Ent("10", 14), 14));
        programa.Instrucciones.Add(new NodoEntradaSalida("puts", new List<NodoAST> { Id("total", 15) }, 15));

        semantico.ComprobarTipos(programa);
        semantico.ImprimirErrores();

        // 3) Solo si no hay errores se asignan offsets y nombres para el ensamblador
        if (semantico.TieneErrores || erroresLexicos.TieneErrores)
        {
            Console.WriteLine("Hay errores léxicos o semánticos: no se genera código objeto.");
            return;
        }

        tabla.AsignarOffsets();
        tabla.ImprimirTabla();
        Console.WriteLine($"Variables en .DATA: {tabla.BytesVariables} bytes (los temporales t1..tN empiezan en {tabla.BytesVariables:X4}h)");
    }

    // ------------------------------------------------------------------
    //  Pipeline completo desde el texto fuente: el AST lo construye el parser, ya no a mano.
    //  Esperado:
    //    1) Ejercicio 5      -> 3 errores semánticos (líneas 5, 9 y 16)
    //    2) if/elsif/else    -> 1 error semántico (línea 5)
    //    3) Programa base    -> 0 errores, 48 tokens, tabla con offsets 0000h y 0002h
    //    4) Errores de sintaxis -> 2 errores sintácticos (líneas 3 y 4); el semántico no se ejecuta
    //    5) if/elsif/else válido -> 0 errores, 21 cuádruplos; etiquetas L1->21, L2->10, L3->18;
    //                               temporales t1..t6 (Dec, Whole, Dec, Whole, Dec, Dec); cadenas c1="Mitad: ", c2="\n", c3="fin"
    // ------------------------------------------------------------------
    static void PipelineCompleto()
    {
        Compilar("1) Ejercicio 5 desde texto", string.Join("\n", new[]
        {
            "start",
            "  # Declaración de variables",
            "  whole contador",
            "  dec total",
            "  whole contador                 # Error 1 (Declaración doble)",
            "  contador = 1",
            "  total = 0.5",
            "",
            "  desconocida = 10               # Error 2 (Variable no declarada)",
            "  total = desconocida + 1        # Prueba anti-cascada",
            "",
            "  while contador <= 5",
            "    total = total + 1.5",
            "    contador = contador + 1",
            "  end",
            "  total = \"hola\"                 # Error 3 (Tipos incompatibles)",
            "  puts total",
            "finish"
        }) + "\n");
        Console.WriteLine();

        Compilar("2) if / elsif / else, gets y puts con lista", string.Join("\n", new[]
        {
            "start",
            "  whole x",
            "  dec total",
            "  gets x",
            "  gets y",
            "  if x == 5",
            "    puts \"Mitad: \", x",
            "  elsif x != 2",
            "    total = total + 3",
            "  else",
            "    puts \"fin\"",
            "  end",
            "finish"
        }) + "\n");
        Console.WriteLine();

        Compilar("3) Programa base", string.Join("\n", new[]
        {
            "start",
            "  # Declaración de variables",
            "  whole contador",
            "  dec total",
            "",
            "  contador = 1",
            "  total = 0.5",
            "",
            "  while contador <= 5",
            "    total = total + 1.5",
            "    contador = contador + 1",
            "  end",
            "",
            "  total = 10",
            "  puts total",
            "finish"
        }) + "\n");
        Console.WriteLine();

        Compilar("4) Errores de sintaxis", string.Join("\n", new[]
        {
            "start",
            "  whole x",
            "  x = = 5                        # Error sintáctico 1: falta el operando",
            "  while x <",
            "    x = x + 1",
            "  end",
            "  puts x",
            "finish"
        }) + "\n");
        Console.WriteLine();

        Compilar("5) Generación: if / elsif / else, puts con lista y conversiones", string.Join("\n", new[]
        {
            "start",
            "  whole x",
            "  dec total",
            "  gets x",
            "  total = x",
            "  if x == 5",
            "    puts \"Mitad: \", x",
            "  elsif x >= total",
            "    total = total + 3",
            "  else",
            "    puts \"fin\"",
            "  end",
            "finish"
        }) + "\n");
    }

    static void Compilar(string titulo, string fuente)
    {
        Console.WriteLine($"--- {titulo} ---");

        // 1) Léxico (llena la tabla de símbolos)
        var tabla = new TablaSimbolosManager();
        var erroresLexicos = new ManejadorErrores();
        var tokens = new ScannerEngine(fuente, erroresLexicos, tabla).EscanearTodo();
        Console.WriteLine($"Tokens reconocidos: {tokens.Count}");
        erroresLexicos.ImprimirErrores();

        // 2) Sintáctico (construye el AST)
        var parser = new ParserEngine(tokens);
        NodoPrograma programa = parser.Analizar();
        parser.ImprimirErrores();

        if (erroresLexicos.TieneErrores || parser.TieneErrores)
        {
            Console.WriteLine("Hay errores léxicos o sintácticos: no se ejecuta el análisis semántico.");
            return;
        }

        // 3) Semántico (misma tabla de símbolos)
        var semantico = new AnalizadorSemantico(tabla);
        semantico.ComprobarTipos(programa);
        semantico.ImprimirErrores();

        if (semantico.TieneErrores)
        {
            Console.WriteLine("Hay errores semánticos: no se genera código objeto.");
            return;
        }

        // 4) Código intermedio (Fase 4): solo llega aquí un programa sin errores
        var generador = new GeneradorCuadruplos(semantico);
        ResultadoFase4 resultado = generador.Generar(programa);

        // 5) Offsets para el ensamblador (los temporales van después de las variables)
        tabla.AsignarOffsets();
        resultado.Temporales.AsignarOffsets(tabla.BytesVariables);

        resultado.Imprimir();
        resultado.GuardarArchivo("salida.quad");
        Console.WriteLine("\nCuádruplos guardados en salida.quad");
        tabla.ImprimirTabla();
        Console.WriteLine($"Variables en .DATA: {tabla.BytesVariables} bytes (los temporales t1..tN empiezan en {tabla.BytesVariables:X4}h)");
    }
}
