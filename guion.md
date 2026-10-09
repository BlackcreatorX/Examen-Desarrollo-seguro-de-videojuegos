# Guion para video: guardado con Chain of Responsibility en Unity

**Duración estimada:** 5–7 minutos  
**Material en pantalla:** Unity, `GuardadoSeguroCoR.cs`, consola de Unity y `README-GuardadoSeguro.md`.

---

## 1. Introducción — 0:00

**En pantalla:** Título: “Guardado de datos con Chain of Responsibility”.

**Narración:**

“En este video voy a explicar un subsistema de guardado para Unity que usa el patrón Chain of Responsibility, o cadena de responsabilidad. Los datos pasan por varios pasos ordenados: validación, transformación y guardado como JSON. También veremos la transformación reversible y qué demuestra la prueba.”

## 2. Contexto y datos — 0:30

**En pantalla:** Abrir `SaveData` y `SaveContext` en `GuardadoSeguroCoR.cs`.

**Narración:**

“`SaveData` representa los datos del juego del ejemplo: monedas y nivel. `SaveContext` es el objeto que viaja por la cadena. Contiene el estado original, el valor transformado, la ruta del archivo y un indicador de validez. Si un eslabón encuentra un problema, puede marcar el contexto como inválido y registrar el error.”

## 3. Cadena de responsabilidad — 1:10

**En pantalla:** Mostrar `SaveHandler`, `SetNext` y `Handle`.

**Narración:**

“`SaveHandler` es la clase base abstracta de los eslabones. `SetNext` conecta el eslabón siguiente y lo devuelve, así podemos configurar la cadena de forma fluida. `Handle` procesa el contexto y lo envía al siguiente eslabón si sigue siendo válido.”

**En pantalla:** Mostrar la composición en `SecureSavePipeline`.

```csharp
validator.SetNext(Obfuscator).SetNext(new Persister());
```

**Narración:**

“El orden queda explícito: primero Validator, luego Obfuscator y al final Persister.”

## 4. Validación — 1:50

**En pantalla:** Mostrar `Validator.Process`.

**Narración:**

“Validator comprueba que existan los datos, que haya una ruta y que el nivel no sea negativo. Si algo falla, establece `IsValid` en falso y la cadena se detiene antes de transformar o escribir. Si pasa la validación, calcula una etiqueta sencilla de integridad para incluirla en el JSON.”

## 5. Transformación y rotación — 2:30

**En pantalla:** Mostrar `Obfuscator`, `RotateKeys`, `Transform` e `Inverse`.

**Narración:**

“Obfuscator aplica una red Feistel de ocho rondas al valor de monedas. Cada ronda mezcla las mitades del número mediante operaciones enteras. Para invertir la operación se recorren las rondas en orden contrario. No se convierte el valor a coma flotante, por lo que la transformación no pierde precisión.”

“Las subclaves aleatorias viven en memoria. `RotateKeys` las sustituye por otras nuevas. La demostración conserva temporalmente una copia de las claves anteriores para invertir el dato que fue transformado antes de la rotación.”

“Esta técnica es ofuscación, no cifrado criptográfico, así que no debe usarse como protección de información sensible.”

## 6. Guardado JSON — 3:35

**En pantalla:** Mostrar `Persister.Process`.

**Narración:**

“Persister crea la carpeta si hace falta, forma un objeto JSON con el valor transformado, el nivel y la etiqueta, y escribe el archivo. La ruta de ejemplo se construye bajo `Application.persistentDataPath`, que Unity ofrece para datos persistentes de la aplicación.”

## 7. Ejecutar la prueba — 4:05

**En pantalla:** Ejecutar la escena que contiene `SavePipelineDemo` y abrir la consola de Unity.

**Narración:**

“La demostración crea el pipeline, guarda un valor original y luego rota las claves activas. Con la copia temporal de las claves anteriores, recupera el valor transformado. En la consola comparamos ambos valores y vemos que `iguales` es `True`.”

**En pantalla:** Acercar el mensaje de `Debug.Log` y la ruta del archivo.

**Narración:**

“El segundo mensaje muestra dónde quedó el archivo JSON.”

## 8. GameManager y límites — 4:45

**En pantalla:** Mostrar el UML y el fragmento de integración en `README-GuardadoSeguro.md`.

**Narración:**

“En una integración, GameManager puede conservar una instancia de `SecureSavePipeline` y llamarla cuando necesite guardar. El diagrama UML muestra esa relación y cómo se conectan los eslabones.”

“Las claves son efímeras y no se guardan. Por eso este ejemplo no puede recuperar el archivo después de cerrar la aplicación. Para persistencia entre ejecuciones se necesita cifrado autenticado y una clave gestionada de forma segura por la plataforma. Guardar la clave junto al JSON anularía gran parte de la protección.”

## 9. Cierre — 5:30

**En pantalla:** Mostrar el diagrama de secuencia.

**Narración:**

“En resumen, Chain of Responsibility separa el guardado en pasos con responsabilidades concretas: validar, transformar y persistir. El contexto transporta los datos, y la prueba confirma que la transformación se invierte exactamente con las claves correspondientes. Gracias por ver el video.”

---

## Tomas sugeridas

- Muestra la consola después de ejecutar la escena para que se lea `iguales=True`.
- Abre el JSON generado para conectar la explicación con la persistencia local.
- Al explicar la rotación, deja visible que la prueba conserva una copia temporal de las claves antiguas.
- Si necesitas acortar el video, resume las secciones 2 y 8, pero conserva la explicación sobre las claves efímeras.
