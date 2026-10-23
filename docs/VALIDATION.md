# Validação da versão 1.2.0

Verificações realizadas em 22/10/2026, em Windows x64 com Wallpaper Engine ativo em 32 bits e FFmpeg/ffprobe 9.0.2.

## Automação

- Restore NuGet em modo bloqueado.
- Build Release com avisos tratados como erros: zero erros e zero avisos.
- **52 testes aprovados**, incluindo mídia H.264 real gerada durante os testes.
- Consulta de vulnerabilidades NuGet para dependências diretas e transitivas sem alertas conhecidos na data da verificação.

Os testes de regressão cobrem falha de log/notificação após finalizar a entrada, cancelamento durante codificação, falha de restauração/limpeza, liberação da próxima conversão, rejeição de MP4 vazio, correspondência de duração/resolução/frames, continuidade automática e descarte assíncrono.

```powershell
dotnet restore WallpaperSceneExporter.sln --locked-mode -p:Platform=x64
dotnet build WallpaperSceneExporter.sln -c Release -p:Platform=x64 --no-restore -warnaserror
dotnet test WallpaperSceneExporter.sln -c Release -p:Platform=x64 --no-build --no-restore
dotnet list WallpaperSceneExporter.sln package --vulnerable --include-transitive --no-restore
```

## Sessão gráfica real

| Cenário | Resultado |
| --- | --- |
| Exportar MP4 | H.264, 480 × 480, 10 segundos, 600 frames no arquivo; validado com ffprobe. |
| Exportar GIF | 480 × 480, 10 segundos, 300 frames; validado pelo aplicativo e por ffprobe. |
| Cancelar uma captura | Entrada restaurada com SHA-256 idêntico ao original; nenhum novo resultado ou temporário remanescente. |
| Fechar durante codificação | Processo encerrado após recuperar a entrada; hash preservado e temporários limpos. |
| Selecionar MP4 nas configurações | Preset de GIF desativado e mensagem correspondente exibida. |
| Interface principal | Nome completo e botão de seleção sem corte na janela padrão. |

Os FPS acima descrevem o arquivo codificado. A quantidade de frames únicos da cena depende da renderização do Wallpaper Engine e do hardware; o capturador registra repetições no log.

A captura utilizada para testar a conversão e seus arquivos de mídia não integram o repositório ou a release. A imagem do README mostra somente a interface do aplicativo.

## Distribuição

O ZIP contém somente aplicativo autocontido, configuração padrão, instruções, instalador do FFmpeg, manifesto e licenças. Cenas, entradas, resultados, logs, caches e executáveis do FFmpeg ficam fora do pacote público. `SHA256SUMS.txt` acompanha o ZIP.

A automação Windows do GitHub verifica o código a partir de um checkout limpo. Ela não substitui a sessão gráfica real, pois os runners não possuem a instalação licenciada do Wallpaper Engine usada nos testes manuais.
