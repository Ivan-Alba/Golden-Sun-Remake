public enum HitFXType
{
    None,
    // Tipos Físicos Básicos
    Slash,          // Corte (Espadas, Dagas, Cimitarras)
    Piercing,       // Estocada/Perforación (Lanzas, Flechas)
    Blunt,          // Impacto contundente (Mazas, Bastones, Puños)

    // Tipos Elementales Directos
    PhysicalVenus,  // Impacto de Tierra/Roca
    PhysicalMars,   // Impacto de Fuego/Chispa
    PhysicalJupiter,// Impacto Eléctrico/Viento
    PhysicalMercury,// Impacto de Hielo/Agua

    // Tipos Especiales
    Custom          // Si la habilidad/unleash requiere un Prefab exclusivo asignado a mano
}