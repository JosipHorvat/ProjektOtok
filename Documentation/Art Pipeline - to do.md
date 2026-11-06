# Art Pipeline - To Do

Potrebno je napraviti sljedeće:

1. **Odrediti veličinu lika na ekranu i broj Unita po visini ekrana:**
   - Definirati točan omjer visine glavnog lika u odnosu na `3840 × 2160 px` (4K) referentni ekran.
   - Odabrati konačni PPU i visinu kamere (npr. `10.8 unita` / `Orthographic Size = 5.4` uz `200 PPU` ili `10 unita` / `Orthographic Size = 5.0` uz `216 PPU`).
2. **Odlučiti o tehnici skeletalne animacije (`PENDING`):**
   - Odabrati između *Mesh Deformation* i čistog *Cutout* pristupa (ili hibrida: *Cutout* za udove + *Mesh Deformation* za plašt/odjeću).
3. **Pretražiti Unity Asset Store za gotova tehnička rješenja:**
   - **Spline-based platforme:** Provjeriti Asset Store alternative u usporedbi s ugrađenim `Unity 2D Sprite Shape` paketom.
   - **Sekundarna fizika kostiju (*Secondary Motion*):** Istražiti *Boing Kit*, *Spring Bones / Dynamic Bone 2D* za automatsko njihanje plašta, kose i lanaca.
   - **Osvjetljenje, Shaderi i Atmosfera:** Istražiti *All In 1 Sprite Shader*, *Volumetric Fog & Mist 2*, *HAZE* i *Smart Lighting 2D*.
   - **Game Feel:** Provjeriti *FEEL* za efekte udaraca i kamere.
4. **Odabrati i testirati rješenje za 2D rasvjetu i Normal mape:**
   - Usporediti **Opciju A** (poluautomatsko generiranje `_n.png` normal mapa kroz ComfyUI Batch pipeline) i **Opciju B** (custom URP Shader Graph koji računa *Rim Light / Bevel* iz Alpha kanala bez dodatnih tekstura).
5. **Definirati strukturu foldera i konvenciju imenovanja:**
   - Uspostaviti standardnu hijerarhiju u `Assets/Art/...` (unutar Unityja) i radnom `RawArt/` folderu (za izvorne Affinity i `.kra` datoteke).
6. **Izraditi custom Krita Python skripte / plugin:**
   - **Auto-Export za pojedinačne sprajtove:** Direktno spremanje `.png` u Unity folder pri `Ctrl+S` uz čuvanje radnog `.kra` i osvježavanje Krita *File Layer* preview kompozicije.
   - **1-Click `.psd` Exporter za likove:** Privremena kopija dokumenta u memoriji koja *flattena* sve top-level grupe u čiste layere i izvozi `.psd` u Unity za `2D PSD Importer`.
7. **Postaviti ComfyUI Batch Folder pipeline:**
   - Složiti workflow koji učitava pojedinačne PNG-ove iz Unity foldera za AI restilizaciju uz očuvanje Alpha kanala.
8. **Postaviti osnovni 2D Scene Template u Unityju:**
   - Konfigurirati *Sorting Layere* i planove paralakse (1–2 forplana, glavni plan, do 8 planova pozadine), `Cinemachine` kameru s `2×` zoom-inom te bazne postavke *Sprite Atlasa* (`Padding = 8`, `Generate Mip Maps = Enabled`, `Tight Packing`).
