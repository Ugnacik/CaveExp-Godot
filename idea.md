# Názov hry
**CaveExp** (Pracovný názov)

# Žáner
Rogue-like / 2D Akčný platformer

# Zhrnutie (Logline)
Hra je kópiou hry *Spelunky*, v ktorej hráč zostupuje do procedurálne generovaných jaskýň plných nebezpečenstva, zradných pascí a nepriateľských tvorov. Cieľom je prežiť, zorientovať sa v bludisku a nájsť východ z každej úrovne.

# Príbeh a zasadenie
Hlavný hrdina je odvážny prieskumník a lovec pokladov, ktorý sa vydáva do temných a nebezpečných jaskynných systémov. Každá jaskyňa je iná a skrýva nové nástrahy. Jediným cieľom je prežiť a bezpečne sa dostať na koniec levelu, aby mohol zostúpiť do ďalšej, ešte nebezpečnejšej časti podzemia.

# Hrateľnosť a mechaniky
* **Procedurálne generované bludisko:** Levely nie sú fixné. Systém dynamicky generuje a spája rôzne typy miestností (slepé uličky, vertikálne šachty, horizontálne chodby) a vytvára tak zakaždým unikátnu a nepredvídateľnú trasu.
* **Presný platforming:** Hráč sa musí spoľahnúť na presné skákanie, vyhýbanie sa okrajom útesov a manévrovanie v stiesnených priestoroch.
* **Nepriatelia a AI:**
  * *Netopiere:* Lietajúce hrozby, ktoré sa objavujú v blízkosti stropov a prepadávajú nič netušiacich prieskumníkov.
  * *Hady / Pozemní nepriatelia:* Hliadkujú na plošinách. Majú jednoduchú AI – chodia dopredu a pri náraze do steny, iného nepriateľa alebo na okraji útesu sa otáčajú.
* **Pasce a prostredie:** Okrem nepriateľov predstavujú hrozbu aj ostré ostne a hlboké priepasti.
* **Systém zranení:** Hráč aj nepriatelia majú systém zdravia (HP). Kontakt s nepriateľom spôsobuje zranenie, čo núti hráča byť opatrný a premýšľať nad každým pohybom.
* **Bezpečné zóny:** Vstupná miestnosť (Entrance) je navrhnutá tak, aby bola úplne bez nepriateľov a poskytla hráčovi bezpečný priestor na začiatku úrovne.

# Vizuálny štýl
* **Grafika:** Retro 2D pixel-art (veľkosť dlaždíc 16x16 pixelov), ktorý evokuje klasické rogue-like hry a dodáva jej špecifickú atmosféru.
* **Dizajn úrovní:** Temné jaskyne s kontrastnými prvkami, vďaka ktorým sú nebezpečné prvky (ako sú ostne) jasne čitateľné.

# Použité technológie
* **Herný engine:** Godot Engine
* **Programovací jazyk:** C# (používaný pre logiku hry, generovanie levelov, správu entít a AI nepriateľov)
* **Dátové formáty:** JSON (pre definície, rozloženie a pravidlá jednotlivých miestností)

# Cieľová skupina
Hráči, ktorí majú radi výzvy, rogue-like hry (ako Spelunky, Dead Cells alebo Enter the Gungeon) a nevadí im občasné zlyhanie, ktoré je prirodzenou súčasťou učenia sa a skúmania nových úrovní.
