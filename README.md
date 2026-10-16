# Remote Chase

On-rails shooter 3D em Unity. Você pilota um drone interceptador por uma
metrópole vertical: o percurso avança sozinho e acelera sem parar, enquanto
você desvia da arquitetura com o teclado e abate drones hostis com a mira
livre no mouse.

Curso Superior de Tecnologia em Jogos Digitais.
**Grupo:** Francisco Casimiro, Pedro Roquette, Rafael Moura, Theo Xavier,
Ramune Bortolin.

## Como jogar

| Comando | Ação |
|---|---|
| `W` `A` `S` `D` | Mover o drone no plano da tela |
| Mouse | Mirar (retículo independente do movimento) |
| Clique esquerdo | Atirar |
| `Shift` + direção | Dash, com invulnerabilidade breve |
| `Espaço` / `Ctrl` | Subir e descer |
| `ESC` | Pausa, opções e volume |
| `R` | Reposicionar o drone no trilho |

**O tiro só sai reto quando o drone está parado.** Manobrando, o disparo
desvia na direção do movimento — o retículo abre e fica vermelho para avisar.
Parar para mirar é o risco que paga a precisão.

### Objetivo

Concluir as duas fases. Você perde se a **blindagem** zerar (colisões e tiros
inimigos) ou se a **bateria** acabar — colete células pelo caminho:

| Célula | Efeito |
|---|---|
| Verde | Restaura blindagem |
| Azul | Escudo defletor temporário |
| Amarela | Recarrega a bateria |

### Cheats de avaliação

| Tecla | Efeito |
|---|---|
| `F1` | Modo Deus: imune a dano e a bateria não esgota |
| `F2` | Pula para a próxima fase |
| `F3` | Trava a velocidade na inicial, para inspecionar o cenário |

## Abrir o projeto

Requer **Unity 6000.4.10f1** — exatamente essa versão. O `ProjectVersion.txt`
a fixa; abrir com outra faz o Unity reserializar cenas e prefabs, o que gera
conflitos de merge difíceis de resolver.

As cenas e prefabs usam merge do próprio Unity. Cada pessoa precisa registrar
a ferramenta **uma vez por máquina**, senão dois commits na mesma cena geram
um conflito praticamente irresolvível:

```bash
git config --global merge.unityyamlmerge.name "Unity YAML merge"
```

```bash
git config --global merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
```

A cena de jogo é `Assets/Scenes/Corrida.unity` e o jogo abre pela
`Assets/Scenes/Menu.unity`.

> `Tools > Remote Chase > PERIGO - Recriar cena Corrida do zero` existe, mas
> apaga toda a interface montada à mão. Só use para recomeçar de fato.

## Gerar build

Com o editor **fechado**:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -batchmode -projectPath . -executeMethod RemoteChase.EditorTools.BuildScript.BuildWindows -outputPath "Builds\Windows\RemoteChase.exe" -logFile -
```

Trocar por `BuildScript.BuildWebGL` ou `BuildScript.BuildLinux` para as outras
plataformas. O build usa as cenas marcadas no Build Settings e **não** regenera
a cena — regenerar apagaria a interface.

## Testes

```bash
"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath . -testPlatform EditMode -logFile -
```

São smoke tests: verificam que a cena está no Build Settings e que ela abre
com drone, câmera, regras de fase e integridade. Eles rodam no editor, onde
não há *managed stripping* — por isso não substituem abrir o build e jogar.

## Estrutura do código

```
Assets/Scripts/
├─ Drone/      movimento no trilho, input, inclinação visual
├─ Combat/     mira, arma hitscan, inimigos, coletáveis, vida e bateria
├─ Camera/     câmera do trilho e câmera de voo livre
├─ World/      gerador do corredor, menus
├─ UI/         HUD
└─ Game/       fases, pontuação, vitória e derrota, cheats
Assets/Editor/ geração de cena, build por linha de comando, religação
```

Duas decisões que não são óbvias no código:

**Os inimigos Sentinela são filhos de um objeto que acompanha o drone**, e por
isso ficam parados na tela enquanto o cenário passa — o alvo estilo Aim Lab. O
Drone Enxame, ao contrário, vive em espaço de mundo: como filho, o movimento do
referencial somaria ao mergulho dele.

**O tiro sai da câmera, não do cano.** O retículo é desenhado a partir da
câmera, que fica metros atrás do drone; um raio saindo do cano erra por
paralaxe tudo que não estiver exatamente na distância de convergência.

**`Assets/link.xml` preserva o módulo de física do *managed stripping*.** Sem
ele o IL2CPP remove `SphereCollider` e `CapsuleCollider` do build, e os
inimigos ficam impossíveis de acertar — falha que só aparece no executável,
nunca no editor.
