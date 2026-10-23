# Changelog

## 1.2.1 — 2026-10-22

- Pacote pronto para extrair e abrir o executável, com .NET, FFmpeg e ffprobe incluídos.
- FFmpeg 9.0.2 compilado pelo projeto com x264 e zlib, incluindo fontes completos, licenças e instruções de compilação no mesmo ZIP.
- CI verifica a aplicação com os mesmos executáveis nativos enviados na release.
- Atualizador local também atualiza as ferramentas de mídia, preservando configuração e arquivos do usuário.
- Fechamento com o aplicativo ocioso agenda a chamada final após o evento `Closing`, evitando uma exceção de reentrada do WPF.

## 1.2.0 — 2026-10-22

Primeira publicação pública do **Wallpaper Engine Scene Exporter**.

- Resultados validados permanecem disponíveis mesmo quando logs ou notificações falham após a finalização.
- MP4 validado com ffprobe: exige vídeo H.264, frames legíveis e correspondência à resolução e duração solicitadas.
- O bloqueio de conversão é liberado mesmo quando a recuperação ou limpeza falha; pacotes não recuperados permanecem disponíveis para a próxima abertura.
- Fechamento assíncrono aguarda cancelamento e recuperação sem bloquear a interface.
- GIF é gerado em área temporária e só aparece em `output` depois de validado.
- Processamento automático continua com a próxima entrada, sem repetir indefinidamente uma entrada restaurada após erro.
- Alterações nas dimensões da superfície interrompem a captura com mensagem clara.
- Configurações distinguem os presets de GIF da qualidade fixa de H.264.
- Migração para .NET 10 LTS e FFmpeg 9.0.2 com instalação local verificada por SHA-256.
- Testes de regressão do pipeline, automação Windows no GitHub Actions e pacote portátil com checksum.

A versão 1.1.0 era usada localmente, antes da revisão para publicação.
