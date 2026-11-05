# Generator de Mapare Memorie ROM
## Generator de mapare ROM pentru automate microprogramate

Aplicație consolă C# (.NET 8) care transformă o **organigramă de microinstrucțiuni** într-un **tabel de alocare a memoriei ROM**, pentru automate microprogramate cu **format variabil** și cu **format fix**. A fost făcută ca unealtă pentru studenții la Arhitectura Calculatoarelor (Calculatoare Numerice), ca să automatizeze o lucrare care se face de obicei manual.

## Ce face
* Citește organigrama dintr-un fișier text sau o introduci direct în consolă.
* **Format variabil:** alocă stările, adaugă automat stările suplimentare de salt necondiționat, dimensionează MUX-ul și DMUX-ul și generează microinstrucțiunile M1 (comandă) și M2 (test).
* **Format fix:** generează câte o locație ROM pe bloc, cu câmpurile `Selecție | ADR0 | ADR1 | Comenzi`.
* **Modul „ambele”:** rezolvă ambele variante și compară memoria ROM necesară.
* **Mod ghidat:** pentru cine nu se descurcă la citirea organigramelor, programul pune întrebări pas cu pas, urmărind săgețile de la START, explică greșelile frecvente și verifică totul la final.

## Cerințe

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* Visual Studio 2022 (opțional) sau orice editor

## Rulare

```bash
dotnet run                                    # meniu interactiv
dotnet run -- ghid                            # direct în modul ghidat
dotnet run -- var exemple/curs-variabil.txt   # format variabil, din fișier
dotnet run -- fix exemple/curs-fix.txt        # format fix, din fișier
dotnet run -- ambele exemple/organigrama-MNPR.txt
```

Tipul automatului nu se vede din organigramă (vine din enunțul problemei). Îl poți da ca argument (`var`, `fix`, `ambele`) sau printr-o linie în fișier: `format: fix | variabil | ambele`. Argumentul din linia de comandă are prioritate.

## Sintaxa organigramei

Un bloc pe linie. Primul bloc este START. Numerele sunt cele scrise pe organigramă.

```
id: COMANDA -> urmator          # dreptunghi: execută o comandă
id: TEST ? daca\_1 : daca\_0      # romb: testează o condiție
```

Exemplu (organigrama cu blocurile M, N, P, R și comenzile O1–O4):

```
0: START -> 1
1: M ? 2 : 3
2: N ? 4 : 6
3: P ? 6 : 5
4: O2 -> 7
5: O1 -> 8
6: R ? 7 : 8
7: O4 -> 1
8: O3 -> 3
```

## Exemplu de rezultat (fragment, format variabil)

Pentru organigrama din `exemple/curs-variabil.txt`:

```
Stare  Adr    Date        Bloc    Observatii
0      0000   111 111 11  0       M1 (START) -> stare 1
1      0001   111 010 11  1       M1 (C2) -> stare 2
2      0010   011 0 0101  2       M2 (T3): 1 -> stare 3, 0 -> stare 5
```

Programul afișează și alocarea intrărilor MUX/DMUX, numărul de stări, dimensiunea ROM-ului (de exemplu `2^4 x 8`) și formatul fiecărei microinstrucțiuni.

## Cum funcționează (format variabil)

1. **Cerința de adiacență.** După un bloc comandă urmează, la adresa următoare, succesorul lui; după un bloc test urmează ramura 1. Ramura 0 se accesează prin câmpul de adresă.
2. **Un singur predecesor prin numărare.** Un bloc poate fi atins prin numărare doar din unul dintre predecesori; ceilalți primesc o stare suplimentară de salt necondiționat.
3. **Evitarea ciclurilor.** Dacă lanțurile alese s-ar închide într-un ciclu, programul alege alt predecesor sau transformă una dintre legături în salt.
4. **Ordinea lanțurilor.** START este primul; celelalte lanțuri sunt plasate în ordinea în care sunt descoperite.

START rămâne mereu în starea 0 (resetul registrului).

## Structura proiectului

```
.
├── Program.cs        # intrare, parsare, alocare, afișare, modul ghidat
├── exemple/          # organigrame de test
└── README.md
```

## Limitări
* Un bloc comandă generează o singură comandă (DMUX-ul are o singură ieșire activă).
* Dacă un bloc poate fi atins prin numărare din doi predecesori, ordinea liniilor din fișier decide cine primește saltul; rezultatul este valid, dar poate diferi de alocarea din baremul unei lucrări.
* Nu există încă teste automate.

## Context
Proiectul se bazează pe metoda de alocare prezentată la cursul de Calculatoare Numerice (automate microprogramate cu format fix și variabil). Materialele cursului nu fac parte din acest repository.

## Autor
Modrangu Robert Constantin, student la Facultatea de Electronică, Comunicații și Calculatoare, Universitatea Politehnica București – Centrul Universitar Pitești.

