# # IpShared - Partilha Fácil de Endereços IP

![Language](https://img.shields.io/badge/C%23-Avalonia%20UI-blueviolet.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android-green.svg)
![Status](https://img.shields.io/badge/Status-Ferramenta%20Funcional-orange)

Este projeto nasceu de uma necessidade que antecipei para um projeto futuro maior. Decidi criar o IpShared como uma ferramenta independente para resolver o problema de partilhar endereços IP e, ao mesmo tempo, aproveitar a oportunidade para explorar o desenvolvimento multiplataforma com **C#** e **Avalonia UI**.

A aplicação converte um par IP/Porta em formatos "humanos" (como uma sequência de palavras) ou compactos, simplificando a sua partilha e reduzindo erros na partilha.

## 🚀 Tecnologias Utilizadas
- **Linguagem:** C#
- **Framework de UI:** Avalonia UI (para suporte nativo a Windows, Android, e potencialmente Linux)
- **Controle de Versões:** Git e GitHub
- **Extra:** A base da UI foi gerada experimentalmente com **Inteligência Artificial**, servindo como um estudo de caso sobre as suas capacidades e limitações atuais no desenvolvimento de interfaces.

## 🎯 Objetivo Principal
Este projeto foi guiado por alguns objetivos claros:
1.  **Criar uma Ferramenta Útil:** O objetivo principal foi construir uma aplicação funcional e autónoma que resolvesse um problema real e que pudesse ser usada para apoiar outros projetos.
2.  **Exploração Tecnológica:** O projeto foi um campo de testes para aprender os fundamentos do Avalonia UI, do desenvolvimento multiplataforma e para aprofundar conhecimentos em **manipulação de bits e algoritmos de codificação**.
3.  **Validação do Algoritmo:** A aplicação serviu como um ambiente real para implementar e testar o algoritmo de conversão de dados, que é o núcleo da ferramenta.

## ✔️ A Solução
IpShared oferece uma interface simples para converter um par IP/Porta em vários formatos otimizados para partilha, cada um com um propósito específico:
- **Formato Words (Human-Readable):** O principal diferencial. Transforma os dados numa sequência de 5 palavras fáceis de ditar. Utilizando a **capitalização das letras para codificar metadados** como o ID do idioma e parte da porta, sem adicionar caracteres extras.
- **Formato Base16/Base62:** Gera códigos alfanuméricos curtos, ideais para copiar e colar em chats ou documentos.
- **Código QR:** Apresenta um QR Code com os dados codificados, perfeito para partilha visual e rápida com dispositivos móveis.
- **Default:** O formato clássico `IP:Porta` para referência.

A lógica de conversão está isolada da UI. A secção abaixo detalha a arquitetura técnica do formato "Words".

## ⚙️ Como Funciona: A Codificação do Formato "Words"
O verdadeiro desafio técnico do IpShared foi criar um algoritmo capaz de empacotar de forma reversível 52 bits de dados (32 do IP, 16 da Porta e 4 do ID do Idioma) numa sequência de 5 palavras. Isto foi alcançado através de uma combinação de interleaving e codificação de metadados via capitalização:
1.  **Codificação do ID do Idioma (4 bits):** Os 4 bits que identificam a lista de palavras (permitindo até 16 idiomas) são codificados de forma subtil na **capitalização da primeira letra das primeiras quatro palavras**. Um `1` torna a letra maiúscula; um `0` mantém-na minúscula.
2.  **Codificação dos Metadados da Porta (3 bits):** Parte da informação da porta (os 3 bits menos significativos) é codificada na **capitalização das letras da última palavra**. Um padrão de maiúsculas/minúsculas (ex: `PoTe`) representa diretamente esses bits, permitindo reconstruir parte da porta sem usar espaço extra.
3.  **Empacotamento dos Dados Restantes:** Os dados restantes – 32 bits do IP e 13 bits da porta – são combinados e divididos em "chunks" de 9 bits.
4.  **Mapeamento para Palavras:** Cada "chunk" de 9 bits corresponde a um índice num dicionário de 512 palavras (`2^9`), resultando na sequência final de 5 palavras.

Este método garante que toda a informação necessária é contida numa string curta, legível e robusta, otimizada para comunicação verbal e manual.
Esta abordagem introduz uma **dificuldade conhecida**: a partilha verbal pode tornar-se mais complexa, especialmente ao ditar o padrão de capitalização da última palavra. No entanto, foi uma decisão de design deliberada. As alternativas seriam adicionar uma sexta palavra (comprometendo a brevidade) ou limitar significativamente o intervalo de portas suportado. Optei por esta solução por considerar que a dificuldade de verbalização ocorre apenas em casos específicos, enquanto os benefícios de manter uma string de 5 palavras e suportar toda a gama de portas são permanentes.

## 👤 Meu Papel
Neste projeto, o meu papel foi o de antecipar uma necessidade que teria em um projeto futuro. Em vez de esperar que a partilha de IPs se tornasse um problema, decidi construir uma solução antes do tempo, criando esta ferramenta.

Fui responsável por todo o processo: desde a **identificação da necessidade** e o **design da solução**, até à **implementação do algoritmo de codificação de dados** e ao **desenvolvimento da UI** que constitui a própria ferramenta. Este projeto mostra a minha forma de trabalhar: construir não só as aplicações, mas também as ferramentas que as suportam.

## ⚙️ Principais Desafios
- **Curva de Aprendizagem do Avalonia UI:** Embora semelhante a outros frameworks XAML, o Avalonia tem particularidades na configuração de projetos multiplataforma e na gestão de layouts responsivos.
- **Trabalhar com UI Gerada por IA:** A interface gerada automaticamente, embora um bom ponto de partida, continha bugs de layout e código não idiomático, exigindo uma refatoração significativa para se tornar funcional.
- **Empacotamento de Dados em Bits:** O maior desafio técnico foi criar um algoritmo reversível para empacotar eficientemente não apenas um endereço IP (32 bits), mas também um número de **porta (16 bits)** e um **identificador de idioma (4 bits)** – permitindo até 16 listas de palavras diferentes. Isto exigiu manipulação cuidadosa de bits para garantir que todos os dados fossem codificados e descodificados corretamente dentro do formato "Words".

## ✅ Resultados
- **Protótipo Funcional:** A aplicação está totalmente funcional em Windows e Android, validando a viabilidade da ideia e da tecnologia escolhida.
- **Aprendizagem Acelerada:** O projeto foi uma excelente plataforma para aprender na prática os conceitos do Avalonia UI e do desenvolvimento multiplataforma em .NET.
- **Visão Realista sobre IA em UI:** A experiência proporcionou uma visão clara das capacidades e (atuais) limitações da IA na geração de interfaces, mostrando que a supervisão e intervenção de um desenvolvedor ainda são essenciais.

## 🔮 Próximos Passos
O projeto está em fase inicial e tem um plano claro para o futuro:
- **Melhorar a Experiência de Utilizador em Android:** A lógica atual de copiar e selecionar texto foi herdada da versão de desktop. É crucial refatorar esta parte para implementar uma experiência mais nativa para mobile, como um botão "tocar para copiar", que é mais intuitivo do que a seleção de texto manual em ecrãs táteis.
- **Refatoração Completa da UI:** Substituir o código gerado por IA por uma interface mais limpa, idiomática e robusta.
- **Adicionar Suporte a Novas Plataformas:** Compilar e testar a aplicação para garantir a compatibilidade com **Linux**.
- **Melhorias de Usabilidade:** Adicionar mais opções de conversão e configurações personalizáveis.
