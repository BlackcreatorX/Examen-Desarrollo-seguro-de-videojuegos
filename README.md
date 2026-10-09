# Guardado seguro con Chain of Responsibility (Unity)

El código completo y la prueba con `Debug.Log` están en [GuardadoSeguroCoR.cs](GuardadoSeguroCoR.cs). Coloca el archivo bajo `Assets/Scripts/` en un proyecto Unity y agrega `SavePipelineDemo` a un GameObject para ejecutar el ejemplo. `Persister` escribe `save.json` dentro de `Application.persistentDataPath`.

La cadena se compone así: `Validator → Obfuscator → Persister`. Si Validator rechaza el contexto, `IsValid` queda en `false` y el resto de la cadena no se ejecuta. `SetNext` devuelve el siguiente eslabón para permitir composición fluida.

La transformación es una red Feistel de ocho rondas sobre un `ulong`; su función de ronda contiene productos y mezclas no lineales. La inversa recorre las rondas en orden inverso, sin conversiones de coma flotante ni pérdida de precisión. Las subclaves aleatorias se regeneran en RAM con `RotateKeys()`.

**Límite importante:** esto es ofuscación, no cifrado seguro. El archivo no se puede recuperar después de cerrar el proceso porque las claves son efímeras y no se guardan. En la demostración, se conserva una copia temporal de las claves antiguas en RAM para verificar el dato después de rotar las claves activas. Para guardados durables que deban sobrevivir reinicios, usa cifrado autenticado y una clave gestionada por una plataforma segura; persistir la clave junto al JSON anula la protección. `IntegrityTag` es una comprobación sencilla de corrupción accidental, no una firma contra manipulación deliberada.

## UML de clases

```mermaid
classDiagram
    class GameManager {
      +SaveData CurrentSave
      +SaveGame()
    }
    class SecureSavePipeline {
      -SaveHandler chain
      +Obfuscator Obfuscator
      +Save(data) SaveContext
    }
    class SaveContext {
      +SaveData Raw
      +ulong Transformed
      +bool IsValid
      +string Error
      +string FilePath
      +uint IntegrityTag
    }
    class SaveHandler {
      <<abstract>>
      -SaveHandler next
      +SetNext(handler) SaveHandler
      +Handle(context)
      #Process(context)*
    }
    class Validator
    class Obfuscator {
      -uint[] roundKeys
      +RotateKeys()
      +Transform(value, keys) ulong
      +Inverse(value, keys) ulong
    }
    class Persister
    class SaveData {
      +ulong coins
      +int level
    }
    GameManager --> SecureSavePipeline : solicita guardado
    SecureSavePipeline *-- SaveHandler : configura
    SecureSavePipeline --> Obfuscator : rota claves
    SaveHandler <|-- Validator
    SaveHandler <|-- Obfuscator
    SaveHandler <|-- Persister
    SaveHandler --> SaveContext : procesa
    SaveContext --> SaveData : contiene
```

`GameManager` puede crear `SecureSavePipeline` una vez y llamar a `Save(CurrentSave)`. Por ejemplo:

```csharp
private Juego.Guardado.SecureSavePipeline saves;
private void Awake() => saves = new Juego.Guardado.SecureSavePipeline(
    System.IO.Path.Combine(Application.persistentDataPath, "save.json"));
private void SaveGame() => saves.Save(CurrentSave);
```

## Secuencia de guardado y prueba de rotación

```mermaid
sequenceDiagram
    participant GM as GameManager / Demo
    participant P as SecureSavePipeline
    participant V as Validator
    participant O as Obfuscator
    participant S as Persister
    GM->>P: Save(datos)
    P->>V: Handle(contexto)
    V->>V: validar campos y calcular etiqueta
    alt datos inválidos
      V-->>GM: contexto inválido (cadena detenida)
    else datos válidos
      V->>O: Handle(contexto)
      O->>O: Feistel con subclaves actuales
      O->>S: Handle(contexto transformado)
      S->>S: serializar JSON local
      S-->>GM: contexto guardado
      GM->>O: RotateKeys()
      GM->>O: Restore(transformado, copia temporal antigua)
      O-->>GM: valor original exacto
      GM->>GM: Debug.Log(original == recuperado)
    end
```
