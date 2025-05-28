# 📦 Sistema de Gestión de Almacenes

Este repositorio contiene los tres proyectos principales del sistema de gestión de almacenes:

- 📱 **android-app**: Aplicación móvil Android para operaciones en campo.
- 🌐 **api**: API RESTful desarrollada en .NET Core.
- 💻 **desktop-app**: Aplicación de escritorio en C# para gestión administrativa.

---

## 📁 Estructura del Repositorio

```text
/almacen-system/
├── android-app/   # Proyecto Android (Kotlin)
├── api/           # Proyecto .NET Core (API)
├── desktop-app/   # Proyecto C# (WPF)
└── README.md
```

---

### ✅ API (.NET Core)

```bash
cd api
# Restaurar dependencias
dotnet restore

# Ejecutar la API
dotnet run
```
---

### ✅ Aplicación de escritorio (C# WPF)
Abrir desktop-app/AlmacenDesktop.sln (o el archivo .sln correspondiente) en Visual Studio.

Compilar y ejecutar con F5 o desde el botón de ejecución.

---

### ✅ Aplicación Android
Abrir la carpeta android-app/ con Android Studio.

Sincronizar Gradle si es necesario.

Ejecutar en un emulador o dispositivo Android.

---

### ⚙️ Requisitos
- .NET SDK

- Visual Studio

- Android Studio

- Java JDK (para Android)

- Emulador o dispositivo Android

---

### 📥 Clonar el repositorio
```bash
git clone https://github.com/jmriveroAuroracorp/SGA.git
```
---

### ✅ Buenas prácticas
- Usa mensajes de commit claros. Ejemplos:

  - `feat: agregar funcionalidad de inventario`

  - `fix: corregir error de login`

  - `refactor: optimizar consulta de productos`

- Trabaja en ramas por módulo o funcionalidad.

- Probar siempre los cambios antes de subir.

---

### 📫 Contacto
Para soporte o colaboración, contacta al equipo de desarrollo.
