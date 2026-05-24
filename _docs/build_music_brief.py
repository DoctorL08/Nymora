"""
Génère _docs/Nymora_Brief_Musique.pdf à destination du musicien.
Resize les grandes images, encode tout en base64 dans un HTML, puis appelle
Chrome en mode headless pour produire le PDF.
"""
import base64, io, os, subprocess, sys
from pathlib import Path
from PIL import Image

ROOT = Path(r"C:\Users\Lorenzo\Documents\Unity\Nymora\Nymora")
DOCS = ROOT / "_docs"
HTML_PATH = DOCS / "Nymora_Brief_Musique.html"
PDF_PATH = DOCS / "Nymora_Brief_Musique.pdf"


def img_b64(rel_path: str, max_w: int | None = None) -> str:
    p = ROOT / rel_path
    img = Image.open(p)
    if img.mode not in ("RGB", "RGBA"):
        img = img.convert("RGBA")
    if max_w and img.width > max_w:
        ratio = max_w / img.width
        img = img.resize((max_w, int(img.height * ratio)), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return base64.b64encode(buf.getvalue()).decode("ascii")


# --- Images ---------------------------------------------------------------
sr = img_b64(r"Assets\_Nymora\Art\Sprites\Soulrender\Base\sources\SR_idle_stage0.png", 256)
sr1 = img_b64(r"Assets\_Nymora\Art\Sprites\Soulrender\Base\sources\SR_idle_stage1.png", 256)
sr2 = img_b64(r"Assets\_Nymora\Art\Sprites\Soulrender\Base\sources\SR_idle_stage2.png", 256)
ns = img_b64(r"Assets\_Nymora\Art\Sprites\Nightseer\Avatar\NS_avatar_128px.png", 256)
co = img_b64(r"Assets\_Nymora\Art\Sprites\Colossar\Avatar\colossar_avatar_128px.png", 256)
ne = img_b64(r"Assets\_Nymora\Art\Sprites\Necram\Avatar\necram_avatar_128px.png", 256)
gh = img_b64(r"Assets\_Nymora\Art\Sprites\Ghostra\Avatar\GHOSTRA_Avatar_128px.png", 256)
map_hub = img_b64(r"Assets\_Nymora\Art\Hub\map_hub_sans_fond.png", 1100)
map_combat = img_b64(r"Assets\_Nymora\Art\UI\Maps\Map_Combat_1.png", 1100)


HTML = f"""<!doctype html>
<html lang="fr"><head><meta charset="utf-8">
<title>Nymora — Brief musique</title>
<style>
  @page {{ size: A4; margin: 14mm 14mm 14mm 14mm; }}
  html, body {{ background:#0d0a0f; }}
  body {{
    font-family: 'Segoe UI', 'Helvetica Neue', Arial, sans-serif;
    color:#e8e3d8; margin:0; padding:0; line-height:1.45; font-size:11pt;
  }}
  h1, h2, h3 {{ font-family: 'Cinzel', 'Trajan Pro', 'Georgia', serif; letter-spacing:0.02em; }}
  h1 {{ font-size: 28pt; margin:0 0 4pt 0; color:#f3e9d2;
        text-shadow: 0 0 12px #5a1a1a; }}
  h2 {{ font-size: 15pt; margin: 0 0 6pt 0; color:#d8c79a;
        border-bottom:1px solid #3a2a35; padding-bottom:3pt; }}
  h3 {{ font-size: 11.5pt; margin: 8pt 0 2pt 0; color:#f1d7a3; }}
  .sub {{ color:#a39a8a; font-style:italic; font-size:10pt; }}
  .page {{ padding: 0 0 4mm 0; page-break-after: always; }}
  .page:last-child {{ page-break-after: auto; }}
  .row {{ display:flex; gap:10pt; align-items:flex-start; }}
  .col {{ flex:1; }}
  .pill {{ display:inline-block; padding:1pt 7pt; border-radius:3pt;
          background:#2a2030; color:#e8d3aa; font-size:9pt;
          border:1px solid #4a3a4f; margin: 1pt 3pt 1pt 0; }}
  .palette {{ display:flex; gap:6pt; margin: 4pt 0 6pt 0; }}
  .swatch {{
    flex:1; height:42pt; border-radius:4pt; display:flex; flex-direction:column;
    justify-content:flex-end; padding:4pt 6pt; color:#fff; font-size:8.5pt;
    text-shadow: 0 1px 2px rgba(0,0,0,0.7); border:1px solid #2a2030;
  }}
  .swatch b {{ font-size:9pt; letter-spacing:0.04em; }}
  .swatch .hex {{ font-family: 'Consolas', monospace; opacity:0.85; }}
  .neutrals {{ display:flex; gap:6pt; margin: 2pt 0 0 0; }}
  .neutrals .swatch {{ height: 26pt; font-size:8pt; }}
  .grid-classes {{ display:grid; grid-template-columns: 1fr 1fr; gap:8pt 14pt; }}
  .cls {{ display:flex; gap:9pt; padding:6pt; border:1px solid #2c1f2d;
         border-radius:5pt; background: linear-gradient(180deg, #14101a 0%, #0d0a13 100%); }}
  .cls img {{ width:64pt; height:64pt; image-rendering:pixelated; flex-shrink:0;
              background:#0a0810; border-radius:3pt; padding:2pt;
              border:1px solid #2a2030; }}
  .cls h3 {{ margin:0 0 1pt 0; }}
  .cls .role {{ font-size:9pt; color:#a39a8a; margin-bottom:3pt; font-style:italic; }}
  .cls p {{ margin:2pt 0; font-size:9.5pt; }}
  .cls .mood {{ color:#d8c79a; font-size:9pt; }}
  .stages {{ display:flex; gap:8pt; justify-content:center; margin: 6pt 0; }}
  .stages figure {{ margin:0; text-align:center; }}
  .stages img {{ width:72pt; height:72pt; image-rendering:pixelated;
                  background:#0a0810; padding:3pt; border-radius:4pt;
                  border:1px solid #3a1a1a; }}
  .stages figcaption {{ font-size:8.5pt; color:#a39a8a; margin-top:2pt; }}
  .map-card {{ border:1px solid #2c1f2d; border-radius:5pt; padding:6pt;
               background:#0e0a13; margin-bottom:6pt; }}
  .map-card img {{ width:100%; image-rendering:pixelated; border-radius:3pt;
                    display:block; }}
  .map-card .cap {{ display:flex; justify-content:space-between; align-items:baseline;
                     margin-bottom:4pt; }}
  .map-card h3 {{ margin:0; }}
  .map-card .tag {{ font-size:9pt; color:#a39a8a; font-style:italic; }}
  .track {{ border:1px solid #2c1f2d; border-radius:5pt; padding:7pt 9pt;
            background:#120e18; margin-bottom:5pt; }}
  .track h3 {{ margin:0 0 2pt 0; display:flex; justify-content:space-between; }}
  .track .meta {{ font-size:9pt; color:#a39a8a; font-family:'Consolas', monospace; }}
  .track p {{ margin:2pt 0; font-size:10pt; }}
  ul {{ margin:3pt 0 3pt 16pt; padding:0; }}
  ul li {{ margin:1pt 0; }}
  .quote {{ border-left:3px solid #5a1a1a; padding:2pt 8pt; margin: 6pt 0;
            color:#d8c79a; font-style:italic; font-size:10pt;
            background:#13101a; }}
  .footer {{ font-size:8.5pt; color:#6a6256; text-align:center; margin-top:8pt;
             border-top:1px solid #2a2030; padding-top:4pt; }}
  .kv {{ font-size:10pt; }}
  .kv b {{ color:#e8d3aa; }}
  .free {{ display:inline-block; padding:3pt 7pt; border:1px dashed #6a4f3a;
           border-radius:3pt; color:#e8d3aa; font-size:9pt; margin-top:4pt;
           background:#1a120e; }}
</style></head>
<body>

<!-- ============ PAGE 1 ============ -->
<section class="page">
  <h1>NYMORA</h1>
  <div class="sub">Brief musique — un document de contexte pour t'imprégner du projet avant de composer. Aucune contrainte stylistique imposée ; on te fait confiance pour traduire l'ambiance.</div>

  <div class="quote">
    « Tactical · Asymétrique · Esportable · Dark Fantasy »
  </div>

  <div class="row" style="margin-top:6pt;">
    <div class="col">
      <h2>Le jeu en 30 secondes</h2>
      <p style="font-size:10.5pt;">Nymora est un <b>PvP tactique 1v1 au tour par tour</b> en pixel-art dark fantasy. Vue plongée sur une grille, matches courts (15–20 min), 6–8 tours, 1500 HP, 8 PA / 3 PM, 6 sorts par deck + 1 sort signature à débloquer. <b>Cinq classes radicalement asymétriques</b> : on ne joue pas le même jeu selon la classe choisie.</p>
      <p class="kv">
        <span class="pill">Solo dev</span>
        <span class="pill">Unity 2022.3 LTS</span>
        <span class="pill">Alpha Windows mai 2027</span>
        <span class="pill">F2P + cosmétiques (zéro pay-to-win)</span>
      </p>
    </div>
  </div>

  <h2 style="margin-top:10pt;">Ambiance & ton</h2>
  <p style="font-size:10.5pt;">Dark fantasy, gothique, occulte. <b>Pas heroic fantasy clinquante</b>, pas grimdark trash non plus : on est dans le rituel, la sorcellerie, l'invocation, le sang qui parle. L'univers respire l'arène ancienne, les cathédrales en ruines, les ombres allongées par les torches. Les classes sont des <i>archétypes vivants</i> — le berserker en hémorragie, le voyant qui voit ce qu'on ne voit pas, le colosse qui referme l'espace, le mage infectieux, l'assassin spectral.</p>

  <p style="font-size:10.5pt;">Références art validées (pour cadrer l'œil et l'oreille) : <b>Blasphemous</b> (mood gothique, contraste, gravité), <b>Owlboy</b> (lighting, lisibilité), <b>Hyper Light Drifter</b> (économie de pixels, posture), <b>Children of Morta</b> (combat top-down), <b>Eastward</b> (richesse d'ambiance).</p>

  <h2 style="margin-top:10pt;">Palette globale — couleurs d'accent par classe</h2>
  <div class="sub">Verrouillées dans le code. Donnent à chaque classe son identité chromatique — utiles si tu veux teinter les motifs musicaux par classe.</div>
  <div class="palette">
    <div class="swatch" style="background:#B22222;"><b>SOULRENDER</b><span class="hex">#B22222 — rouge sang</span></div>
    <div class="swatch" style="background:#6A4FB6;"><b>NIGHTSEER</b><span class="hex">#6A4FB6 — violet voilé</span></div>
    <div class="swatch" style="background:#7A6B5C;"><b>COLOSSAR</b><span class="hex">#7A6B5C — terre/pierre</span></div>
    <div class="swatch" style="background:#5A8B3E;"><b>NECRAM</b><span class="hex">#5A8B3E — vert putride</span></div>
    <div class="swatch" style="background:#6F8FA8;"><b>GHOSTRA</b><span class="hex">#6F8FA8 — bleu spectral</span></div>
  </div>

  <h3>Neutres dominants (fonds, UI, ambiance générale)</h3>
  <div class="neutrals">
    <div class="swatch" style="background:#0d0a0f; color:#d8c79a;">noir profond · #0d0a0f</div>
    <div class="swatch" style="background:#2a2030; color:#e8d3aa;">violet sombre · #2a2030</div>
    <div class="swatch" style="background:#3a2a35; color:#e8d3aa;">prune cendre · #3a2a35</div>
    <div class="swatch" style="background:#d8c79a; color:#2a2030;">or vieilli · #d8c79a</div>
    <div class="swatch" style="background:#f3e9d2; color:#2a2030;">parchemin · #f3e9d2</div>
  </div>

  <h2 style="margin-top:10pt;">Évolution visuelle — exemple Soulrender (les 3 stages de la ressource Hémoglyphe)</h2>
  <div class="sub">Toutes les classes ont 3 stages visuels indexés sur leur ressource : <b>repos → mid → cap</b>. C'est exactement le genre de progression qu'on peut transposer en intensité musicale : un combat n'a pas un seul niveau d'énergie.</div>
  <div class="stages">
    <figure><img src="data:image/png;base64,{sr}"><figcaption>Stage 0 — Repos<br>aura discrète</figcaption></figure>
    <figure><img src="data:image/png;base64,{sr1}"><figcaption>Stage mid<br>rouge marqué</figcaption></figure>
    <figure><img src="data:image/png;base64,{sr2}"><figcaption>Stage cap (5/5)<br>fissures écarlates</figcaption></figure>
  </div>

  <div class="footer">Nymora — Brief musique · v1 · 19 mai 2026</div>
</section>


<!-- ============ PAGE 2 ============ -->
<section class="page">
  <h2>Les 5 classes — fantasy de gameplay</h2>
  <div class="sub">Chaque classe ne change pas le <i>score</i> du combat — elle change la <b>nature</b> du combat. Un duel Soulrender vs Nightseer ne se joue pas sur la même grille mentale qu'un Colossar vs Ghostra.</div>

  <div class="grid-classes" style="margin-top:8pt;">

    <div class="cls">
      <img src="data:image/png;base64,{sr}" alt="Soulrender">
      <div>
        <h3>SOULRENDER <span class="mood">· rouge sang</span></h3>
        <div class="role">Le Berserker sanguinaire</div>
        <p>Prédateur en hémorragie permanente. Il <b>raccourcit le match</b>. Pas de long combat, il refuse qu'il en existe un. À chaque tour, il pousse l'adversaire vers le précipice.</p>
        <p class="mood">Émotion ressentie en face : <b>panique</b>. Une horloge biologique qui tambourine.</p>
      </div>
    </div>

    <div class="cls">
      <img src="data:image/png;base64,{ns}" alt="Nightseer">
      <div>
        <h3>NIGHTSEER <span class="mood">· violet voilé</span></h3>
        <div class="role">Le Prédateur manipulateur</div>
        <p>Ne tue pas, il <b>conduit</b>. Pose des marques, voile des cases, ment sur la carte. Les deux joueurs ne voient pas la même map.</p>
        <p class="mood">Émotion ressentie en face : <b>paranoïa</b>. On doute de tout, même de ses propres yeux.</p>
      </div>
    </div>

    <div class="cls">
      <img src="data:image/png;base64,{co}" alt="Colossar">
      <div>
        <h3>COLOSSAR <span class="mood">· terre / pierre</span></h3>
        <div class="role">Le Colosse oppressant</div>
        <p>Ne veut pas tuer vite — il veut <b>suffoquer</b>. Pose des piliers, des murs, rétrécit l'arène. Au tour 5, il ne reste plus que 8 cases libres.</p>
        <p class="mood">Émotion ressentie en face : <b>oppression</b>, enfermement. Une montagne qui marche.</p>
      </div>
    </div>

    <div class="cls">
      <img src="data:image/png;base64,{ne}" alt="Necram">
      <div>
        <h3>NECRAM <span class="mood">· vert putride</span></h3>
        <div class="role">Le Mage infectieux</div>
        <p>Ne combat pas — il <b>inocule</b>. Chaque sort est une condamnation à retardement. La map devient toxique. Existences brèves dans l'arène.</p>
        <p class="mood">Émotion ressentie en face : <b>fatalité</b>. Un cadavre qui marche encore.</p>
      </div>
    </div>

    <div class="cls" style="grid-column: 1 / -1;">
      <img src="data:image/png;base64,{gh}" alt="Ghostra">
      <div>
        <h3>GHOSTRA <span class="mood">· bleu spectral</span></h3>
        <div class="role">L'Assassin spectral</div>
        <p>Ne combat pas dans le présent. Pose des <b>leurres</b> (clones visuels identiques à elle-même) sur la grille. L'adversaire joue contre 4 silhouettes dont 3 sont fausses. Le combat n'est plus une lecture de positions — c'est un puzzle temporel.</p>
        <p class="mood">Émotion ressentie en face : <b>peur mentale</b>. L'incapacité à lire ce qui est réel.</p>
      </div>
    </div>

  </div>

  <h2 style="margin-top:10pt;">Boucle de jeu côté joueur</h2>
  <div class="row">
    <div class="col">
      <h3>1 · Login</h3>
      <p style="font-size:9.5pt;">Écran d'entrée. Logo Nymora, fond animé. Très court — quelques secondes avant clic.</p>
    </div>
    <div class="col">
      <h3>2 · Hub communautaire</h3>
      <p style="font-size:9.5pt;">Cathédrale en ruines, espace social en multi (chat, déplacements, défis entre joueurs). On s'attarde. PNJ marchands, tableau d'événements. <b>Chill mais habité.</b></p>
    </div>
    <div class="col">
      <h3>3 · Combat (IA ou ranked)</h3>
      <p style="font-size:9.5pt;">Arène grille top-down. <b>15s par tour</b>, 6–8 tours. Le tempo monte tour après tour. Le sort signature s'allume quand la ressource est au cap.</p>
    </div>
  </div>

  <div class="footer">Nymora — Brief musique · v1 · 19 mai 2026</div>
</section>


<!-- ============ PAGE 3 ============ -->
<section class="page">
  <h2>Les deux lieux musicaux</h2>

  <div class="map-card">
    <div class="cap">
      <h3>Hub communautaire — cathédrale en ruines</h3>
      <span class="tag">scène <code>10_CommunityHub</code> · multi Photon Fusion</span>
    </div>
    <img src="data:image/png;base64,{map_hub}" alt="Map hub communautaire">
    <p style="font-size:9.5pt; margin-top:4pt;">Vue ¾ isométrique-light, à la Stardew Valley / Eastward. Cathédrale dévastée, statues brisées, torches, bannières, fontaines. C'est <b>le lieu où le joueur traîne entre deux matchs</b> : il discute, regarde le tableau d'événements, défie un ami, déambule. L'ambiance doit donner envie de <b>rester</b>, pas de partir vite. Dark fantasy contemplative — quelque chose de sacré sous la ruine.</p>
  </div>

  <div class="map-card">
    <div class="cap">
      <h3>Arène de combat — grille tactique</h3>
      <span class="tag">scènes <code>30_CombatIA</code>, <code>40_CombatRanked</code> · Photon Quantum (déterministe)</span>
    </div>
    <img src="data:image/png;base64,{map_combat}" alt="Map arène de combat">
    <p style="font-size:9.5pt; margin-top:4pt;">Vue plongée top-down sur une grille. Le combat est <b>tactique, lent en surface, brutal au moment des coups</b>. Les classes <i>modifient la grille</i> en temps réel : du sang coagulé, des piliers de pierre, des cases voilées, de la brume toxique, des leurres spectraux. La musique vit avec ça : on est dans un duel rituel où chaque tour compte.</p>
  </div>

  <h2 style="margin-top:6pt;">Les 3 morceaux demandés</h2>
  <div class="sub">Indications fonctionnelles uniquement. <b>Aucune contrainte stylistique imposée</b> — choix d'instruments, de tempo, de genre, d'époque sonore : libres. On veut ta lecture du monde.</div>

  <div class="track">
    <h3>1 · Login loop <span class="meta">≈ 30 sec · loop seamless</span></h3>
    <p>Premier contact avec Nymora. Très court, doit <b>boucler proprement</b> sans qu'on l'entende boucler. Pose l'identité sonore du jeu en moins d'une demi-minute.</p>
    <div class="free">Libre à toi sur la direction.</div>
  </div>

  <div class="track">
    <h3>2 · Hub — exploration / social <span class="meta">≈ 3–4 min · loop</span></h3>
    <p>Musique du temps long. Le joueur traîne, lit, discute. <b>Doit pouvoir tourner 30 minutes sans fatiguer</b>. Habité mais discret — la cathédrale n'est pas vide, mais on n'est pas en danger.</p>
    <div class="free">Libre à toi sur la direction.</div>
  </div>

  <div class="track">
    <h3>3 · Combat <span class="meta">≈ 3–4 min · loop</span></h3>
    <p>Musique du duel. Le match dure 15–20 minutes en pointe, mais un combat <i>type</i> tient en 3–4 min, donc le loop doit <b>monter en pression sans s'épuiser</b>. Le sort signature qui débloque, la jauge qui se remplit, le tour 6 qui arrive : la musique accompagne sans dicter.</p>
    <div class="free">Libre à toi sur la direction.</div>
  </div>

  <h2 style="margin-top:6pt;">Contraintes techniques (les seules)</h2>
  <ul>
    <li>Format : <b>WAV ou OGG</b>, 44.1 kHz / 16-bit minimum.</li>
    <li>Loops : marqueurs de boucle propres, ou point de loop documenté.</li>
    <li>Mix : niveau cohérent entre les 3 morceaux, headroom ~ -6 dB, pas de master limité à fond.</li>
    <li>Stems : si possible, livrer aussi les <b>stems séparés</b> (rythmique / mélodique / textures) — utile pour mixer in-game.</li>
  </ul>

  <div class="footer">Nymora — Brief musique · v1 · 19 mai 2026 · Lorenzo (dev) · contact : lorenzobiau1998@gmail.com</div>
</section>

</body></html>
"""

HTML_PATH.write_text(HTML, encoding="utf-8")
print(f"HTML écrit : {HTML_PATH}  ({HTML_PATH.stat().st_size//1024} KB)")


# --- Chrome -> PDF --------------------------------------------------------
chrome_candidates = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
]
chrome = next((c for c in chrome_candidates if os.path.exists(c)), None)
if not chrome:
    print("Aucun Chrome / Edge trouvé — HTML généré mais PDF non produit.")
    sys.exit(0)

url = HTML_PATH.as_uri()
cmd = [
    chrome,
    "--headless=new",
    "--disable-gpu",
    "--no-pdf-header-footer",
    f"--print-to-pdf={PDF_PATH}",
    url,
]
print(f"Lancement Chrome headless : {chrome}")
res = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
if res.returncode != 0:
    print("STDOUT:", res.stdout)
    print("STDERR:", res.stderr)
    sys.exit(res.returncode)

if PDF_PATH.exists():
    print(f"PDF généré : {PDF_PATH}  ({PDF_PATH.stat().st_size//1024} KB)")
else:
    print("Chrome n'a pas produit de PDF.")
    sys.exit(1)
