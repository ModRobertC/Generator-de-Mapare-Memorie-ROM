using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

// Generator de mapare ROM - automate microprogramate
//
// Doua variante:
//   1) FORMAT VARIABIL: numarator 74163 (incrementare / incarcare paralela), MUX 2^m:1 cu GND (salt
//      necondizionat) si 1-logic (numarare), DMUX 1:2^c pentru comenzi, ROM 2^a x W, formate M1 / M2.
//   2) FORMAT FIX: RPP + n MUX 2:1, MUX 2^m:1 pentru teste, ROM 2^n x (m + 2n + p) cu campurile
//      Selectie test | ADR0 | ADR1 | Comenzi; cate o locatie pe bloc.
//
// Sintaxa (un bloc pe linie, primul bloc = START, id-urile = numerotarea din organigrama):
//   id: COMANDA -> urmator          bloc executie comanda (START = bloc fara comanda)
//   id: TEST ? daca1 : daca0        bloc testare conditie (1 -> daca1, 0 -> daca0)
//
// MOD GHIDAT (pentru cine nu se descurca la citirea organigramei):
//   programul te plimba pe organigrama, bloc cu bloc, urmarind sagetile de la START, iti pune intrebari
//   de control (forma, numarul de sageti, unde duce fiecare iesire), explica greselile frecvente si la
//   final verifica totul impreuna cu tine. Dupa aceea rezolva organigrama.
//
// Rulare:  dotnet run                     (meniu: introducere directa sau ghidata)
//          dotnet run -- ghid             (direct in modul ghidat)
//          dotnet run -- fix fisier.txt   (format fix, organigrama din fisier)
//          dotnet run -- var fisier.txt   (format variabil)
//          dotnet run -- ambele fisier.txt (ambele + compararea memoriei ROM)
// Tipul poate fi dat si in text printr-o linie:  format: fix | variabil | ambele
// (organigrama singura nu spune tipul automatului - acesta vine din enuntul problemei).

const string SampleVar = @"0: START -> 1
1: C2 -> 2
2: T3 ? 3 : 4
3: T2 ? 2 : 6
4: C0 -> 7
5: C3 -> 1
6: C1 -> 5
7: T4 ? 8 : 10
8: T1 ? 5 : 9
9: C4 -> 1
10: T5 ? 9 : 4";

const string SampleFix = @"0: START -> 1
1: C2 -> 2
2: T3 ? 3 : 6
3: T2 ? 2 : 4
4: C1 -> 5
5: C4 -> 1
6: C0 -> 7
7: T4 ? 8 : 10
8: T1 ? 5 : 9
9: C5 -> 1
10: T0 ? 9 : 11
11: C3 -> 6";

Console.OutputEncoding = Encoding.UTF8;

Mode? mode = null;
string? path = null;
bool guided = false;
foreach (var arg in args)
{
    if (arg.Equals("fix", StringComparison.OrdinalIgnoreCase)) mode = Mode.Fix;
    else if (arg.Equals("var", StringComparison.OrdinalIgnoreCase)) mode = Mode.Var;
    else if (arg.Equals("ambele", StringComparison.OrdinalIgnoreCase)) mode = Mode.Both;
    else if (arg.Equals("ghid", StringComparison.OrdinalIgnoreCase) || arg.Equals("ghidat", StringComparison.OrdinalIgnoreCase)) guided = true;
    else path = arg;
}

var lines = new List<string>();
if (path != null && File.Exists(path))
{
    lines.AddRange(File.ReadAllLines(path));
}
else
{
    if (path != null) Console.WriteLine($"Fisierul \"{path}\" nu a fost gasit.");
    if (!guided)
    {
        Console.WriteLine("Cum vrei sa introduci organigrama?");
        Console.WriteLine("  1 = direct, prin sintaxa (rapid)");
        Console.WriteLine("  2 = ghidat: te ajut pas cu pas sa citesti organigrama");
        Console.Write("Alege (1/2, Enter = 1): ");
        guided = Console.ReadLine()?.Trim() == "2";
    }

    if (guided)
    {
        try
        {
            lines.AddRange(Tutor.Collect());
        }
        catch (QuitTutor)
        {
            Console.WriteLine("\nAi iesit din modul ghidat.");
            return;
        }
    }
    else
    {
        Console.WriteLine("Sintaxa:  id: COMANDA -> urmator    sau    id: TEST ? daca1 : daca0");
        Console.WriteLine("Primul bloc este START (ex: 0: START -> 1).");
        Console.WriteLine("Optional, o linie  format: fix | variabil | ambele  alege tipul automatului.");
        Console.WriteLine("Introdu organigrama (linie goala = gata; Enter direct = exemplul din curs):");
        string? l;
        while (!string.IsNullOrWhiteSpace(l = Console.ReadLine()))
            lines.Add(l);
    }
}

// Linia "format: ..." din text (argumentele din linia de comanda au prioritate)
var dirRx = new Regex(@"^\s*format\s*:\s*(fix|variabil|var|ambele)\s*$", RegexOptions.IgnoreCase);
Mode? fromText = null;
for (int i = lines.Count - 1; i >= 0; i--)
{
    var dm = dirRx.Match(lines[i]);
    if (!dm.Success) continue;
    if (fromText == null)
    {
        var word = dm.Groups[1].Value.ToLowerInvariant();
        fromText = word == "fix" ? Mode.Fix : word == "ambele" ? Mode.Both : Mode.Var;
    }
    lines.RemoveAt(i);
}
mode ??= fromText;

if (mode == null)
{
    if (guided)
    {
        Console.WriteLine("\nTipul automatului nu se vede din organigrama - il gasesti in enuntul problemei:");
        Console.WriteLine("  1 = format variabil: numarator 74163, MUX cu GND si 1-logic, DMUX pentru comenzi, microinstructiuni M1 / M2");
        Console.WriteLine("  2 = format fix: RPP + n MUX 2:1, o locatie ROM pe bloc, campuri Selectie | ADR0 | ADR1 | Comenzi");
        Console.WriteLine("  3 = rezolva ambele variante si compara memoria ROM");
    }
    else
    {
        Console.WriteLine("Tip automat microprogramat:  1 = format variabil,  2 = format fix,  3 = compara ambele");
    }
    Console.Write("Alege (1/2/3): ");
    var choice = Console.ReadLine()?.Trim();
    mode = choice == "2" ? Mode.Fix : choice == "3" ? Mode.Both : Mode.Var;
}

if (lines.Count == 0)
{
    string sample = mode == Mode.Fix ? SampleFix : SampleVar;
    lines.AddRange(sample.Split('\n'));
    Console.WriteLine(sample + "\n");
}

try
{
    Automat.Run(lines, mode.Value);
}
catch (FormatException ex)
{
    Console.WriteLine("Eroare: " + ex.Message);
}

enum Mode { Var, Fix, Both }

sealed record Block(string Id, bool IsTest, string? Sig, string Yes, string? No);

// Kind 'N' = starea blocului; 'J' = stare suplimentara de salt necondizionat (dupa blocul B, catre B.Yes)
sealed record Word(char Kind, Block B);

sealed class NaturalComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        var a = Regex.Split(x ?? "", @"(\d+)");
        var b = Regex.Split(y ?? "", @"(\d+)");
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            int c;
            if (long.TryParse(a[i], out var na) && long.TryParse(b[i], out var nb)) c = na.CompareTo(nb);
            else c = string.CompareOrdinal(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }
}

// ============================================================================================
//  MOD GHIDAT
// ============================================================================================
sealed class RedoBlock : Exception { }   // "refa": reia descrierea blocului curent
sealed class QuitTutor : Exception { }   // "iesire": renunta la modul ghidat

static class Tutor
{
    static readonly Regex IdRx = new(@"^\w+$");
    static bool canRedo;

    const string ShapeHelp =
        "Forma blocului iti spune ce face:\n" +
        "  dreptunghi = bloc de COMANDA  (se executa o comanda, iese o singura sageata)\n" +
        "  romb       = bloc de TESTARE  (se verifica o conditie, ies doua sageti: 1 si 0)\n" +
        "Ovalul START il tratam separat, la inceput.";

    const string ArrowHelp =
        "Numara doar sagetile care PLEACA din bloc, nu pe cele care intra in el.\n" +
        "  1 sageata  -> dreptunghi (comanda)\n" +
        "  2 sageti   -> romb (test), marcate cu 0 si 1";

    const string CmdHelp =
        "Comanda este textul scris in interiorul dreptunghiului, de exemplu C2.\n" +
        "Scrie-o exact ca pe organigrama, fara spatii.";

    const string TestHelp =
        "Conditia testata este textul scris in interiorul rombului, de exemplu T3.\n" +
        "Scrie doar numele ei, fara '?' sau alte simboluri.";

    const string NextHelp =
        "Pune degetul pe sageata care iese din dreptunghi si urmareste-o pana intra intr-un bloc.\n" +
        "Daca sageata se uneste cu o alta sageata, continua pe sageata comuna pana la bloc.\n" +
        "Scrie numarul blocului in care intra (nu numele comenzii).";

    const string BranchHelp =
        "Fiecare iesire din romb are langa ea o marcare: 1 (conditia e adevarata / DA) sau 0 (falsa / NU).\n" +
        "Urmareste sageata marcata pana la blocul in care intra si scrie NUMARUL acelui bloc.\n" +
        "Cea mai frecventa greseala: inversarea lui 0 cu 1.";

    // ---------------------------------------------------------------- intrare
    static string Ask(string prompt, string help, Func<string, string?>? validate = null)
    {
        while (true)
        {
            Console.Write(prompt + " ");
            var s = Console.ReadLine();
            if (s == null) throw new QuitTutor();
            s = s.Trim();
            var low = s.ToLowerInvariant();
            if (low is "iesire" or "exit") throw new QuitTutor();
            if (low == "refa")
            {
                if (canRedo) throw new RedoBlock();
                Console.WriteLine("   -> 'refa' functioneaza doar cat descrii un bloc.");
                continue;
            }
            if (low is "?" or "ajutor")
            {
                foreach (var h in help.Split('\n')) Console.WriteLine("   | " + h);
                continue;
            }
            var err = validate?.Invoke(s);
            if (err != null)
            {
                Console.WriteLine("   -> " + err);
                continue;
            }
            return s;
        }
    }

    static bool YesNo(string prompt, bool def)
    {
        while (true)
        {
            Console.Write(prompt + " ");
            var s = Console.ReadLine();
            if (s == null) throw new QuitTutor();
            s = s.Trim().ToLowerInvariant();
            if (s.Length == 0) return def;
            if (s is "d" or "da" or "y" or "yes") return true;
            if (s is "n" or "nu" or "no") return false;
            if (s is "iesire" or "exit") throw new QuitTutor();
            Console.WriteLine("   -> Raspunde cu d (da) sau n (nu).");
        }
    }

    static string? ValidId(string s)
    {
        if (!IdRx.IsMatch(s))
            return "Scrie doar numarul blocului, de exemplu 3 (fara spatii sau alte simboluri).";
        if (int.TryParse(s, out var v) && v > 255)
            return "Numarul pare prea mare pentru o organigrama de curs. Verifica numarul scris langa bloc.";
        return null;
    }

    static string? ValidSig(string s)
    {
        if (!IdRx.IsMatch(s))
            return "Scrie doar denumirea, fara spatii si fara simboluri (de exemplu C2 sau T3).";
        if (s.Equals("START", StringComparison.OrdinalIgnoreCase))
            return "START este ovalul de inceput, nu o comanda sau o conditie. Aici scrie ce se afla in dreptunghi / romb.";
        return null;
    }

    static string AskSignal(string prompt, bool isTest)
    {
        var s = Ask(prompt, isTest ? TestHelp : CmdHelp, ValidSig).ToUpperInvariant();
        bool odd = isTest ? s.StartsWith('C') : s.StartsWith('T');
        if (odd && !YesNo(
                $"   Atentie: in exemplele din curs comenzile se noteaza C.., iar conditiile testate T.. - tu ai scris '{s}' intr-un {(isTest ? "romb" : "dreptunghi")}. E corect asa? [d/N]",
                false))
            throw new RedoBlock();
        return s;
    }

    // ---------------------------------------------------------------- un bloc
    static Block Describe(string id, string from)
    {
        Console.WriteLine($"\n=== Blocul {id} ===  ({from})");
        Console.WriteLine($"Gaseste pe organigrama blocul cu numarul {id} (numarul este scris langa bloc).");
        canRedo = true;
        try
        {
            while (true)
            {
                try { return DescribeOnce(id); }
                catch (RedoBlock) { Console.WriteLine("   Reluam blocul de la inceput."); }
            }
        }
        finally { canRedo = false; }
    }

    static Block DescribeOnce(string id)
    {
        while (true)
        {
            var shape = Ask("Ce forma are blocul?  1 = dreptunghi   2 = romb :", ShapeHelp,
                s => (s is "1" or "2") ? null : "Raspunde cu 1 (dreptunghi) sau 2 (romb). Scrie ? daca vrei ajutor.");

            var arrows = Ask("Cate sageti PLEACA din bloc (nu cele care intra)?", ArrowHelp,
                s => (s is "1" or "2") ? null : "Dintr-un bloc pleaca una sau doua sageti. Daca nu vezi nicio iesire, urmareste conturul blocului: in organigramele din curs fiecare bloc are cel putin o iesire.");

            if (shape != arrows)
            {
                Console.WriteLine(shape == "1"
                    ? "   Nu se potriveste: un dreptunghi are o SINGURA sageata de iesire. Daca din bloc pleaca doua sageti marcate 0 si 1, el este de fapt un romb (test). Mai uita-te o data."
                    : "   Nu se potriveste: un romb are DOUA iesiri (0 si 1). Daca pleaca o singura sageata, blocul este de fapt un dreptunghi (comanda). Mai uita-te o data.");
                continue;
            }

            if (shape == "1")
            {
                var sig = AskSignal("Ce comanda scrie in dreptunghi? (ex: C2)", false);
                var next = Ask("In ce bloc intra sageata care iese din dreptunghi (numarul blocului)?", NextHelp, ValidId);
                if (next == id && !YesNo("   Atentie: sageata se intoarce in acelasi bloc. Sigur asa arata organigrama? [d/N]", false))
                    throw new RedoBlock();
                Console.WriteLine($"   Am inteles:  {id}: {sig} -> {next}");
                Console.WriteLine($"   adica: se executa comanda {sig}, apoi se trece in blocul {next}.");
                if (!YesNo("Corect? [Enter = da, n = reiau blocul]", true)) throw new RedoBlock();
                return new Block(id, false, sig, next, null);
            }
            else
            {
                var sig = AskSignal("Ce conditie scrie in romb? (ex: T3)", true);
                Console.WriteLine("   Din romb pleaca doua sageti, marcate 1 si 0 (uneori DA / NU).");
                var yes = Ask("Urmareste sageata marcata 1 (conditia e adevarata): in ce bloc intra?", BranchHelp, ValidId);
                var no = Ask("Urmareste sageata marcata 0 (conditia e falsa): in ce bloc intra?", BranchHelp, ValidId);
                if (yes == no && !YesNo("   Atentie: ambele iesiri duc in acelasi bloc, deci testul nu ar mai schimba nimic. Ai citit bine marcajele 0 / 1? Pastrez asa? [d/N]", false))
                    throw new RedoBlock();
                Console.WriteLine($"   Am inteles:  {id}: {sig} ? {yes} : {no}");
                Console.WriteLine($"   adica: daca {sig} = 1 se trece in blocul {yes}, iar daca {sig} = 0 se trece in blocul {no}.");
                if (!YesNo("Corect? [Enter = da, n = reiau blocul]", true)) throw new RedoBlock();
                return new Block(id, true, sig, yes, no);
            }
        }
    }

    // ---------------------------------------------------------------- explicatii
    static void Intro()
    {
        Console.WriteLine();
        Console.WriteLine("=== MOD GHIDAT: cum citim organigrama ===");
        Console.WriteLine("O organigrama are trei feluri de blocuri:");
        Console.WriteLine("  ( START )  oval        - punctul de plecare; din el iese o singura sageata, nu executa nicio comanda");
        Console.WriteLine("  [ C2 ]     dreptunghi  - bloc de COMANDA: se executa comanda scrisa inauntru, apoi se merge pe singura sageata");
        Console.WriteLine("  < T3 >     romb        - bloc de TESTARE: se verifica conditia scrisa inauntru; are DOUA iesiri,");
        Console.WriteLine("                           marcate 1 (conditia e adevarata) si 0 (conditia e falsa)");
        Console.WriteLine("Fiecare bloc are un numar scris langa el - acesta este starea lui.");
        Console.WriteLine();
        Console.WriteLine("Metoda: pornim din START si urmarim sagetile, bloc cu bloc. Eu iti spun ce bloc sa cauti,");
        Console.WriteLine("tu te uiti pe organigrama si imi spui ce vezi. Asa nu ratam niciun bloc.");
        Console.WriteLine();
        Console.WriteLine("Comenzi utile la orice intrebare:");
        Console.WriteLine("  ?      = explicatie pentru intrebarea curenta");
        Console.WriteLine("  refa   = reiei blocul curent de la inceput (cand descrii un bloc)");
        Console.WriteLine("  iesire = renunti la modul ghidat");
    }

    // ---------------------------------------------------------------- fluxul principal
    public static List<string> Collect()
    {
        Intro();

        var done = new Dictionary<string, Block>();
        var order = new List<string>();
        var pending = new List<string>();
        var origin = new Dictionary<string, string>();
        string startId = "0";

        void Queue(string target, string from)
        {
            if (done.ContainsKey(target) || pending.Contains(target)) return;
            pending.Add(target);
            origin[target] = from;
        }

        void Accept(Block b)
        {
            done[b.Id] = b;
            if (!order.Contains(b.Id)) order.Add(b.Id);
            if (b.IsTest)
            {
                Queue(b.Yes, $"ajungi aici din blocul {b.Id}, iesirea 1");
                Queue(b.No!, $"ajungi aici din blocul {b.Id}, iesirea 0");
            }
            else
            {
                string src = b.Sig == null ? "START" : $"blocul {b.Id}";
                Queue(b.Yes, $"ajungi aici din {src}, singura iesire");
            }
            Console.WriteLine(pending.Count > 0
                ? $"\n   Mai ai de descris: {string.Join(", ", pending)}"
                : "\n   Ai urmarit toate sagetile pornind de la START.");
        }

        void Drain()
        {
            while (pending.Count > 0)
            {
                var id = pending[0];
                pending.RemoveAt(0);
                Accept(Describe(id, origin[id]));
            }
        }

        // ---- pasul 1: numarul de blocuri (control incrucisat la final)
        Console.WriteLine("\n--- Pasul 1: cate blocuri are organigrama? ---");
        Console.WriteLine("Numara ovalul START, toate dreptunghiurile si toate romburile.");
        var cnt = Ask("Cate blocuri sunt in total? [Enter = nu sunt sigur]",
            "Numara ovalul START + toate dreptunghiurile + toate romburile.\n" +
            "Daca numerele blocurilor merg de la 0 la n, atunci sunt n+1 blocuri.\n" +
            "Daca nu esti sigur, apasa Enter - verificam la final.",
            s => (s.Length == 0 || (int.TryParse(s, out var v) && v >= 2 && v <= 200))
                ? null : "Scrie un numar intreg (minim 2) sau apasa Enter.");
        int? expected = null;
        if (cnt.Length > 0) expected = int.Parse(cnt);

        // ---- pasul 2: START
        Console.WriteLine("\n--- Pasul 2: START ---");
        Console.WriteLine("START este ovalul din care incepe organigrama; nu executa nicio comanda.");
        startId = Ask("Ce numar are START? [Enter = 0]",
            "START este ovalul de la inceputul organigramei. In curs el are numarul 0 (registrul se reseteaza in starea 0).",
            s => s.Length == 0 ? null : ValidId(s));
        if (startId.Length == 0) startId = "0";
        if (startId != "0" && int.TryParse(startId, out _) &&
            !YesNo($"   In curs START are numarul 0, tu ai scris {startId}. Pastrez {startId}? [d/N]", false))
            startId = "0";

        var sid = startId;
        var first = Ask("Din START iese o singura sageata. In ce bloc intra ea (numarul blocului)?", NextHelp,
            s => ValidId(s) ?? (s == sid ? "START nu poate trimite sageata in el insusi." : null));
        Accept(new Block(startId, false, null, first, null));

        // ---- pasul 3: plimbarea pe organigrama
        Console.WriteLine("\n--- Pasul 3: parcurgem blocurile urmarind sagetile ---");
        Drain();

        // ---- pasul 4: control incrucisat dupa numarul de blocuri
        while (expected != null && done.Count != expected.Value)
        {
            Console.WriteLine($"\nAi spus ca organigrama are {expected} blocuri, dar eu am gasit {done.Count} urmarind sagetile din START.");
            Console.WriteLine("Posibilitati:");
            Console.WriteLine("  1 = mai exista blocuri la care nu am ajuns (le adaugi tu acum)");
            Console.WriteLine("  2 = m-am inselat la numarat, ramane cum am descris");
            Console.WriteLine("  3 = vreau sa verific blocurile descrise (tabelul de verificare)");
            var ch = Ask("Alege 1/2/3:",
                "Daca un bloc nu are nicio sageata care sa intre in el, urmarind din START nu ajung la el - il poti adauga la 1.\n" +
                "Daca ai numarat gresit, alege 2. Daca banuiesti ca ai gresit o sageata, alege 3.",
                s => (s is "1" or "2" or "3") ? null : "Raspunde cu 1, 2 sau 3.");
            if (ch == "1")
            {
                var raw = Ask("Scrie numerele blocurilor ramase, separate prin spatiu sau virgula:",
                    "Exemplu: 5 8", s => s.Length > 0 ? null : "Scrie cel putin un numar.");
                var added = false;
                foreach (var t in Regex.Split(raw, @"[,;\s]+").Where(u => u.Length > 0))
                {
                    if (ValidId(t) != null) { Console.WriteLine($"   -> '{t}' nu e un numar de bloc valid, il ignor."); continue; }
                    if (done.ContainsKey(t) || pending.Contains(t)) { Console.WriteLine($"   -> Blocul {t} este deja descris."); continue; }
                    Queue(t, "adaugat de tine, nu ajunge nicio sageata la el din blocurile de pana acum");
                    added = true;
                }
                if (added) Drain();
            }
            else if (ch == "2") expected = done.Count;
            else break;
        }

        // ---- pasul 5: verificare
        List<Block> Sorted()
        {
            var rest = order.Where(i => i != startId).Select(i => done[i]).ToList();
            if (rest.All(b => int.TryParse(b.Id, out _)))
                rest = rest.OrderBy(b => int.Parse(b.Id)).ToList();
            return new[] { done[startId] }.Concat(rest).ToList();
        }

        void Trace(int val)
        {
            var seen = new HashSet<string>();
            var steps = new List<string>();
            var cur = startId;
            while (done.ContainsKey(cur) && seen.Add(cur))
            {
                var b = done[cur];
                steps.Add(b.IsTest ? $"{b.Id}:{b.Sig}={val}" : $"{b.Id}:{b.Sig ?? "START"}");
                cur = b.IsTest ? (val == 1 ? b.Yes : b.No!) : b.Yes;
            }
            steps.Add($"{cur} (ciclu)");
            Console.WriteLine($"  toate conditiile = {val}:  " + string.Join(" -> ", steps));
        }

        void Review()
        {
            Console.WriteLine("\n=== Pasul 5: verificare ===");
            Console.WriteLine("Bloc  Forma        Continut  Iesiri");
            foreach (var b in Sorted())
            {
                string shape = b.IsTest ? "romb" : b.Sig == null ? "oval START" : "dreptunghi";
                string exits = b.IsTest ? $"1 -> {b.Yes}   0 -> {b.No}" : $"-> {b.Yes}";
                Console.WriteLine(b.Id.PadRight(6) + shape.PadRight(13) + (b.Sig ?? "-").PadRight(10) + exits);
            }

            var seen = new HashSet<string> { startId };
            var stack = new Stack<string>();
            stack.Push(startId);
            while (stack.Count > 0)
            {
                var b = done[stack.Pop()];
                foreach (var t in b.IsTest ? new[] { b.Yes, b.No! } : new[] { b.Yes })
                    if (done.ContainsKey(t) && seen.Add(t)) stack.Push(t);
            }
            var lost = order.Where(i => !seen.Contains(i)).ToList();
            if (lost.Count > 0)
                Console.WriteLine($"\nAtentie: din START nu se poate ajunge la blocurile {string.Join(", ", lost)}. " +
                                  "Verifica daca ai notat corect iesirile blocurilor care ar trebui sa duca spre ele.");

            Console.WriteLine("\nTraseu de control (merge pe iesirea 1 / 0 la fiecare romb):");
            Trace(1);
            Trace(0);
            Console.WriteLine("Urmareste aceleasi sageti pe organigrama de pe hartie: treci prin aceleasi blocuri?");
        }

        while (true)
        {
            Review();
            var fix = Ask("\nDaca ceva nu se potriveste, scrie numarul blocului de corectat. Enter = totul e corect:",
                "Daca traseul de mai sus difera de organigrama, cauta primul bloc din traseu pe care il parcurgi altfel\n" +
                "si corecteaza-l: de obicei acolo e o sageata citita gresit (sau 0 cu 1 inversate).",
                s => (s.Length == 0 || done.ContainsKey(s)) ? null : "Nu am un bloc cu numarul asta in lista de mai sus.");
            if (fix.Length == 0) break;
            if (fix == startId)
            {
                // START nu are forma/continut de descris, doar sageata de iesire
                var sid2 = startId;
                var nf = Ask("Din START iese o singura sageata. In ce bloc intra ea (numarul blocului)?", NextHelp,
                    s => ValidId(s) ?? (s == sid2 ? "START nu poate trimite sageata in el insusi." : null));
                Accept(new Block(startId, false, null, nf, null));
            }
            else
            {
                Accept(Describe(fix, "il corectezi"));
            }
            Drain();
        }

        // ---- rezultat: forma textuala
        var result = Sorted().Select(b => b.IsTest
            ? $"{b.Id}: {b.Sig} ? {b.Yes} : {b.No}"
            : $"{b.Id}: {b.Sig ?? "START"} -> {b.Yes}").ToList();

        Console.WriteLine("\nOrganigrama in forma textuala (asa o poti introduce si direct, fara ghid):");
        foreach (var l in result) Console.WriteLine("  " + l);

        if (YesNo("\nSalvez organigrama in fisierul organigrama.txt? [d/N]", false))
        {
            try
            {
                File.WriteAllLines("organigrama.txt", result);
                Console.WriteLine("Salvat. O poti rula cu:  dotnet run -- organigrama.txt");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine("Nu am putut salva fisierul: " + ex.Message);
            }
        }
        return result;
    }
}

static class Automat
{
    static readonly Regex CmdRx = new(@"^(\w+)\s*:\s*(\w+)\s*->\s*(\w+)$");
    static readonly Regex TestRx = new(@"^(\w+)\s*:\s*(\w+)\s*\?\s*(\w+)\s*:\s*(\w+)$");

    static int Bits(int n) => n <= 1 ? 1 : 32 - BitOperations.LeadingZeroCount((uint)(n - 1));
    static string Bin(int v, int w) => Convert.ToString(v, 2).PadLeft(w, '0');

    public static void Run(IEnumerable<string> lines, Mode mode)
    {
        var order = Parse(lines);
        if (mode == Mode.Fix)
        {
            PrintFixed(order);
            return;
        }
        var words = Allocate(order);
        Print(order, words);
        if (mode == Mode.Both)
        {
            Console.WriteLine("\n==============================================\n");
            PrintFixed(order);
            Compare(order, words);
        }
    }

    // Starea = id-ul blocului daca toate id-urile sunt numere distincte, altfel ordinea liniilor
    static Dictionary<string, int> StateCodes(List<Block> order)
    {
        var parsed = order.Select(b => int.TryParse(b.Id, out var v) ? v : -1).ToList();
        bool numeric = parsed.All(v => v >= 0) && parsed.Distinct().Count() == parsed.Count;
        var code = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++) code[order[i].Id] = numeric ? parsed[i] : i;
        return code;
    }

    static void Compare(List<Block> order, List<Word> words)
    {
        int nt = order.Where(b => b.IsTest).Select(b => b.Sig!).Distinct().Count();
        int nc = order.Where(b => !b.IsTest && b.Sig != null).Select(b => b.Sig!).Distinct().Count();

        int mv = Bits(nt + 2), cv = Bits(nc + 1), av = Bits(words.Count);
        int wv = (Math.Max(mv + cv, mv + av) + 7) / 8 * 8;
        long bitsVar = (1L << av) * wv;

        var code = StateCodes(order);
        int nf = Bits(code.Values.Max() + 1), mf = Bits(nt);
        int wf = mf + 2 * nf + nc;
        long bitsFix = (1L << nf) * wf;

        Console.WriteLine("\nComparatie memorie ROM");
        Console.WriteLine($"  Format variabil: 2^{av} x {wv} = {bitsVar} biti  ({words.Count} stari)");
        Console.WriteLine($"  Format fix     : 2^{nf} x {wf} = {bitsFix} biti  ({order.Count} stari)");
        if (bitsVar == bitsFix) Console.WriteLine("  Memorie egala.");
        else Console.WriteLine("  Mai putina memorie: " + (bitsVar < bitsFix ? "format variabil." : "format fix."));
    }

    // ---------- parsare ----------
    static List<Block> Parse(IEnumerable<string> lines)
    {
        var order = new List<Block>();
        var ids = new HashSet<string>();
        foreach (var raw in lines)
        {
            var l = raw.Trim();
            if (l.Length == 0) continue;
            Block b;
            Match m;
            if ((m = TestRx.Match(l)).Success)
                b = new Block(m.Groups[1].Value, true, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
            else if ((m = CmdRx.Match(l)).Success)
            {
                var sig = m.Groups[2].Value;
                if (sig.Equals("START", StringComparison.OrdinalIgnoreCase)) sig = null;
                b = new Block(m.Groups[1].Value, false, sig, m.Groups[3].Value, null);
            }
            else throw new FormatException($"Linie invalida: \"{l}\"");

            if (!ids.Add(b.Id)) throw new FormatException($"Bloc duplicat: {b.Id}");
            order.Add(b);
        }
        if (order.Count == 0) throw new FormatException("Organigrama este goala.");
        foreach (var b in order)
            foreach (var r in new[] { b.Yes, b.No })
                if (r != null && !ids.Contains(r))
                    throw new FormatException($"Blocul {b.Id} face referire la {r}, care nu exista.");
        return order;
    }

    // ---------- recodificarea starilor (pasul 4 din curs) ----------
    // Cerinta de adiacenta: dupa un bloc comanda urmeaza (S+1) succesorul lui; dupa un bloc test urmeaza
    // ramura 1. Un bloc poate fi precedat prin numarare de un singur bloc; ceilalti predecesori primesc
    // o stare suplimentara de salt necondizionat. START (stare 0) nu poate fi precedat de nimeni.
    static List<Word> Allocate(List<Block> order)
    {
        var map = order.ToDictionary(b => b.Id);
        var idx = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++) idx[order[i].Id] = i;
        string start = order[0].Id;

        var req = order.ToDictionary(b => b.Id, b => b.Yes);
        var cand = new Dictionary<string, List<string>>();
        var chosen = new Dictionary<string, string?>();
        foreach (var b in order)
        {
            cand[b.Id] = b.Id == start
                ? new List<string>()
                : order.Where(u => u.Yes == b.Id).Select(u => u.Id).ToList();
            chosen[b.Id] = cand[b.Id].Count > 0 ? cand[b.Id][0] : null;
        }

        List<string>? FindCycle()
        {
            var seen = new HashSet<string>();
            foreach (var b in order)
            {
                if (seen.Contains(b.Id)) continue;
                var path = new List<string>();
                string? v = b.Id;
                while (v != null && !path.Contains(v) && !seen.Contains(v))
                {
                    path.Add(v);
                    var nx = req[v];
                    v = chosen[nx] == v ? nx : null;
                }
                foreach (var p in path) seen.Add(p);
                if (v != null && path.Contains(v))
                {
                    int k = path.IndexOf(v);
                    return path.GetRange(k, path.Count - k);
                }
            }
            return null;
        }

        List<string>? cy;
        while ((cy = FindCycle()) != null)
        {
            var cs = new HashSet<string>(cy);
            bool fixedIt = false;
            foreach (var v in cy)
            {
                var alt = cand[v].FirstOrDefault(w => !cs.Contains(w));
                if (alt != null) { chosen[v] = alt; fixedIt = true; break; }
            }
            if (!fixedIt)
            {
                var v0 = cy.OrderBy(id => idx[id]).First();
                chosen[v0] = null;
            }
        }

        List<string> Chain(string h)
        {
            var res = new List<string> { h };
            var v = h;
            while (chosen[req[v]] == v) { v = req[v]; res.Add(v); }
            return res;
        }

        var heads = order.Where(b => chosen[b.Id] == null).Select(b => b.Id).ToList();
        var chains = heads.ToDictionary(h => h, Chain);
        var headOf = new Dictionary<string, string>();
        foreach (var kv in chains)
            foreach (var n in kv.Value) headOf[n] = kv.Key;

        // Ordinea lanturilor: START primul, apoi in ordinea in care sunt "descoperite"
        var words = new List<Word>();
        var queue = new List<string> { start };
        var queued = new HashSet<string> { start };
        for (int qi = 0; qi < queue.Count; qi++)
        {
            foreach (var id in chains[queue[qi]])
            {
                var b = map[id];
                words.Add(new Word('N', b));
                var refs = new List<string>();
                if (b.IsTest) refs.Add(b.No!);
                if (chosen[req[id]] != id)
                {
                    words.Add(new Word('J', b));
                    refs.Add(b.Yes);
                }
                foreach (var r in refs)
                {
                    var h = headOf[r];
                    if (queued.Add(h)) queue.Add(h);
                }
            }
        }
        return words;
    }

    // ---------- automat cu FORMAT FIX  ----------
    // O locatie ROM pe bloc. Starea = id-ul blocului daca toate id-urile sunt numere distincte,
    // altfel numerotarea in ordinea liniilor. Locatie: Sel(m) | ADR0(n) | ADR1(n) | Comenzi(p).
    static void PrintFixed(List<Block> order)
    {
        var cmp = new NaturalComparer();
        var tests = order.Where(b => b.IsTest).Select(b => b.Sig!).Distinct().OrderBy(s => s, cmp).ToList();
        var cmds = order.Where(b => !b.IsTest && b.Sig != null).Select(b => b.Sig!).Distinct().OrderBy(s => s, cmp).ToList();

        var code = StateCodes(order);
        var byCode = order.ToDictionary(b => code[b.Id]);

        int n = Bits(code.Values.Max() + 1);   // linii de adresa ROM = biti de stare
        int m = Bits(tests.Count);             // linii de selectie MUX
        int p = cmds.Count;                    // comenzi (cate un bit pe comanda, activ 1)
        int W = m + 2 * n + p;

        string CmdBits(string? sig)
        {
            var arr = new char[p];
            Array.Fill(arr, '0');
            if (sig != null) arr[p - 1 - cmds.IndexOf(sig)] = '1';
            return new string(arr);
        }

        if (code[order[0].Id] != 0)
            Console.WriteLine("Atentie: START ar trebui sa aiba starea 0 (resetul registrului).");

        Console.WriteLine();
        Console.WriteLine($"1) Semnale testate: {tests.Count}  ->  MUX {1 << m}:1 (m = {m})");
        for (int i = 0; i < (1 << m); i++)
        {
            string name = i < tests.Count ? tests[i] : "NC";
            Console.WriteLine($"     intrarea {i}  ({Bin(i, m)})  {name}");
        }

        Console.WriteLine($"\n2) Comenzi: {p}  ->  p = {p} (cate un bit, activ 1-logic)");

        Console.WriteLine($"\n3) Stari: {order.Count}  ->  n = {n}  (RPP pe {n} biti, {n} MUX 2:1)");
        Console.WriteLine($"   ROM: 2^{n} x (m + 2n + p) = {1 << n} x ({m} + {2 * n} + {p}) = {1 << n} x {W}");

        Console.WriteLine("\n4) Alocarea liniilor de date");
        Console.WriteLine($"   D{W - 1}..D{p + 2 * n}: Selectie semnal testat (camp 3)");
        Console.WriteLine($"   D{p + 2 * n - 1}..D{p + n}: ADR0 - starea urmatoare pentru 0 (camp 2)");
        Console.WriteLine($"   D{p + n - 1}..D{p}: ADR1 - starea urmatoare pentru 1 (camp 1)");
        if (p > 0)
            Console.WriteLine($"   D{p - 1}..D0: Comenzi {string.Join(", ", Enumerable.Reverse(cmds))} (camp 0)");

        Console.WriteLine("\n5) Continutul memoriei ROM  (x = don't care)");
        string hdr = "Stare".PadRight(7) + "Adr".PadRight(n + 3) + "Sel ADR0 ADR1 Comenzi".PadRight(W + 6) + "Observatii";
        Console.WriteLine(hdr);
        Console.WriteLine(new string('-', hdr.Length + 14));

        for (int s = 0; s < (1 << n); s++)
        {
            string sel, a0, a1, cm, obs;
            if (byCode.TryGetValue(s, out var blk))
            {
                if (blk.IsTest)
                {
                    sel = Bin(tests.IndexOf(blk.Sig!), m);
                    a0 = Bin(code[blk.No!], n);
                    a1 = Bin(code[blk.Yes], n);
                    cm = CmdBits(null);
                    obs = $"({blk.Sig}): 1 -> {code[blk.Yes]}, 0 -> {code[blk.No!]}";
                }
                else
                {
                    sel = new string('x', m);
                    a0 = a1 = Bin(code[blk.Yes], n);
                    cm = CmdBits(blk.Sig);
                    string name = blk.Sig ?? "START";
                    obs = $"({name}) -> {code[blk.Yes]}";
                }
            }
            else
            {
                sel = new string('x', m);
                a0 = a1 = Bin(0, n);
                cm = CmdBits(null);
                obs = "neutilizat";
            }
            string fields = string.Join(" ", new[] { sel, a0, a1, cm }.Where(f => f.Length > 0));
            Console.WriteLine(s.ToString().PadRight(7) + Bin(s, n).PadRight(n + 3) + fields.PadRight(W + 6) + obs);
        }

        Console.WriteLine($"\nCapacitate automat: max {1 << n} stari, {1 << m} semnale testate, {p} comenzi (extensibil prin largirea ROM-ului).");
    }

    // ---------- afisare ----------
    static void Print(List<Block> order, List<Word> words)
    {
        var cmp = new NaturalComparer();
        var tests = order.Where(b => b.IsTest).Select(b => b.Sig!).Distinct().OrderBy(s => s, cmp).ToList();
        var cmds = order.Where(b => !b.IsTest && b.Sig != null).Select(b => b.Sig!).Distinct().OrderBy(s => s, cmp).ToList();

        int m = Bits(tests.Count + 2);       // linii de selectie MUX (GND + teste + "1")
        int c = Bits(cmds.Count + 1);        // linii de selectie DMUX (o iesire NC pentru START)
        int a = Bits(words.Count);           // linii de adresa ROM / bitii numaratorului
        int wMin = Math.Max(m + c, m + a);
        int W = (wMin + 7) / 8 * 8;          // latimea cuvantului ROM, rotunjita la octet
        int ncCode = (1 << c) - 1;

        var addr = new Dictionary<string, int>();
        for (int i = 0; i < words.Count; i++)
            if (words[i].Kind == 'N') addr[words[i].B.Id] = i;

        var missing = order.Where(b => !addr.ContainsKey(b.Id)).Select(b => b.Id).ToList();
        if (missing.Count > 0)
            Console.WriteLine("Atentie: blocuri nealocate (nu pot fi atinse din START): " + string.Join(", ", missing));

        Console.WriteLine();
        Console.WriteLine($"1) Semnale testate: {tests.Count}  ->  MUX {1 << m}:1 (m = {m})");
        for (int i = 0; i < (1 << m); i++)
        {
            string name;
            if (i == 0) name = "GND (salt necondizionat)";
            else if (i <= tests.Count) name = tests[i - 1];
            else if (i == (1 << m) - 1) name = "1-logic (numarare)";
            else name = "NC";
            Console.WriteLine($"     intrarea {i}  ({Bin(i, m)})  {name}");
        }

        Console.WriteLine($"\n2) Comenzi: {cmds.Count}  ->  DMUX 1:{1 << c} (c = {c})");
        for (int i = 0; i < (1 << c); i++)
        {
            string name = i < cmds.Count ? cmds[i] : "NC";
            Console.WriteLine($"     iesirea {i}  ({Bin(i, c)})  {name}");
        }

        int extra = words.Count(w => w.Kind == 'J');
        Console.WriteLine($"\n3) Stari: {order.Count} blocuri + {extra} stari suplimentare (salturi) = {words.Count}");
        Console.WriteLine($"   Linii de adresa a = {a}  (numarator 74163 pe {a} biti)");
        Console.WriteLine($"   ROM: 2^{a} x {W} = {1 << a} x {W}");

        Console.WriteLine($"\n4) Formatul microinstructiunilor ({W} biti, x = don't care)");
        Console.WriteLine($"   M1 (comanda): {m} biti '1' | Com({c}) | x({W - m - c})");
        Console.WriteLine($"   M2 (test)   : Tst({m}) | x({W - m - a}) | Adr({a})");
        Console.WriteLine("   x se completeaza ca in curs: M1 cu 1, M2 cu 0.");

        Console.WriteLine("\n5) Continutul memoriei ROM");
        string hdr = "Stare".PadRight(7) + "Adr".PadRight(a + 3) + "Date".PadRight(W + 4) + "Bloc".PadRight(8) + "Observatii";
        Console.WriteLine(hdr);
        Console.WriteLine(new string('-', hdr.Length + 20));

        for (int i = 0; i < (1 << a); i++)
        {
            string fields, blk, obs;
            if (i < words.Count)
            {
                var w = words[i];
                var b = w.B;
                if (w.Kind == 'N' && !b.IsTest)
                {
                    int code = b.Sig == null ? ncCode : cmds.IndexOf(b.Sig);
                    int pad = W - m - c;
                    fields = Bin((1 << m) - 1, m) + " " + Bin(code, c) + (pad > 0 ? " " + new string('1', pad) : "");
                    blk = b.Id;
                    string name = b.Sig ?? "START";
                    obs = $"M1 ({name}) -> stare {i + 1}";
                }
                else if (w.Kind == 'N')
                {
                    int sel = tests.IndexOf(b.Sig!) + 1;
                    int tgt = addr[b.No!];
                    int pad = W - m - a;
                    fields = Bin(sel, m) + (pad > 0 ? " " + new string('0', pad) : "") + " " + Bin(tgt, a);
                    blk = b.Id;
                    obs = $"M2 ({b.Sig}): 1 -> stare {i + 1}, 0 -> stare {tgt}";
                }
                else
                {
                    int tgt = addr[b.Yes];
                    int pad = W - m - a;
                    fields = Bin(0, m) + (pad > 0 ? " " + new string('0', pad) : "") + " " + Bin(tgt, a);
                    blk = "(salt)";
                    obs = $"M2 (salt necondizionat la stare {tgt})";
                }
            }
            else
            {
                int pad = W - m - a;
                fields = Bin(0, m) + (pad > 0 ? " " + new string('0', pad) : "") + " " + Bin(0, a);
                blk = "-";
                obs = "neutilizat (salt la 0)";
            }
            Console.WriteLine(i.ToString().PadRight(7) + Bin(i, a).PadRight(a + 3) + fields.PadRight(W + 4) + blk.PadRight(8) + obs);
        }

        Console.WriteLine($"\nCapacitate automat: max {1 << a} stari, {(1 << m) - 2} semnale testate, {(1 << c) - 1} comenzi.");
    }
}
