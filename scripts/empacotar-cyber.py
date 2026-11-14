"""Gera o ZIP de entrega de cyber: código-fonte, auditoria e vídeo/prints do painel.

    python scripts/empacotar-cyber.py          # -> entrega-cyber.zip na raiz do repositório

Fica de fora o que não é fonte (bin/, obj/, .git/, .vs/, .idea/) e qualquer arquivo de segredo local.
"""
import pathlib
import zipfile

RAIZ = pathlib.Path(__file__).resolve().parent.parent
DESTINO = RAIZ / "entrega-cyber.zip"

INCLUIR = ["src", "tests", "docs/cyber", "scripts", "Directory.Build.props", "GlobalSolutionCitizenTech.slnx",
           "Dockerfile", "docker-compose.yml", "README.md", ".gitignore"]
IGNORAR_PASTAS = {"bin", "obj", ".git", ".vs", ".idea", "TestResults", "_video", "__pycache__"}
IGNORAR_ARQUIVOS = {"appsettings.Local.json", ".env"}


def arquivos():
    for item in INCLUIR:
        caminho = RAIZ / item
        if caminho.is_file():
            yield caminho
        elif caminho.is_dir():
            for f in sorted(caminho.rglob("*")):
                if f.is_file() and not IGNORAR_PASTAS.intersection(f.relative_to(RAIZ).parts) \
                        and f.name not in IGNORAR_ARQUIVOS and not f.name.startswith(".env."):
                    yield f


def main() -> None:
    total = 0
    with zipfile.ZipFile(DESTINO, "w", zipfile.ZIP_DEFLATED) as z:
        for f in arquivos():
            z.write(f, f.relative_to(RAIZ).as_posix())
            total += 1
    print(f"{DESTINO.name}: {total} arquivos, {DESTINO.stat().st_size / 1024 / 1024:.1f} MB")


if __name__ == "__main__":
    main()
