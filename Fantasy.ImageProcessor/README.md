# Fantasy.ImageProcessor

API em .NET 8 para normalizar medalhas/emblemas do 4Fantasy.

Ela recebe uma imagem com fundo xadrez falso, remove somente o fundo claro conectado às bordas, recorta a área útil, centraliza em um canvas 1:1 e exporta PNG transparente em 512x512.

## Como rodar

```bash
cd Fantasy.ImageProcessor
dotnet restore
dotnet run
```

Abra:

```text
http://localhost:5140/swagger
```

## Endpoint

```http
POST /api/images/normalize-medal?size=512
```

Envie um `multipart/form-data` com o campo:

```text
file = sua-imagem.png
```

## Exemplo com cURL

```bash
curl -X POST "http://localhost:5140/api/images/normalize-medal?size=512" \
  -F "file=@clubista.png" \
  --output clubista-512x512.png
```

## Observação importante

Esse projeto não usa IA. Ele funciona melhor quando o fundo é claro/cinza em padrão xadrez, como as imagens geradas com falso fundo transparente.

Ele não remove todo branco da imagem. A lógica remove apenas pixels claros/cinzas conectados às bordas, preservando elementos internos como letras brancas, camisas e brilhos.

## TODO
implementar reducao dd cartas em nivel de pixels 
implementar download de cartas via arquivo .zip

