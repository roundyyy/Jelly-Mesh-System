# JellyMesh System by Roundy - User Manual

## Overview

The JellyMesh System is a Unity component that adds jelly-like physics to any mesh. Using Unity's Job System and Burst compiler for high performance, it can handle complex meshes efficiently even in demanding scenes.



## Features

- **High-performance** soft-body deformation using Unity's Job System and Burst compiler
- **Dynamic jelly physics** with customizable parameters
- **Runtime-adjustable pivot point** to change the center of deformation during gameplay
- **Automatic LOD system** to optimize performance based on camera distance
- **Mesh collider support** for physics interactions with the deformed mesh
- **Performance optimization options** to balance between quality and speed
- **Professional editor interface** with detailed tooltips and help sections

## Installation

1. Import the JellyMesh System package into your Unity project
2. Required packages : Jobs, Burst, Collections, Mathematics
3. Ensure your mesh has "Read/Write Enabled" checked in the import settings:
   - Select your mesh in the Project window
   - In the Inspector, click on the Model tab
   - Check "Read/Write Enabled" in the import settings
   - Click Apply
4. Add the JellyMeshSystem component to any GameObject with a MeshFilter and MeshRenderer or SkinnedMeshRenderer

## Quick Start Guide

1. Select a GameObject with a mesh in your scene
2. Add the JellyMeshSystem component (Component → Physics → JellyMesh System)
3. Adjust the "Intensity" slider to control the overall strength of the effect
4. Play the scene to see the jelly effect in action
5. Experiment with the Stiffness and Damping settings to find the desired behavior

## Using a Custom Pivot

For better control over how the mesh deforms, you can specify a custom pivot point:

1. Create an empty GameObject as a child of your mesh object
2. Position it where you want the "center" of the jelly effect to be
3. Assign this GameObject to the "Custom Pivot" field in the JellyMeshSystem component
4. The pivot point can be modified during runtime to dynamically change the center of deformation



## Parameter Reference

### Jelly Physics Settings

* **Intensity (0.1-20)**: Controls the overall strength of the jelly effect. Higher values create more pronounced deformation.
* **Mass (0.1-20)**: Affects how quickly vertices respond to forces. Higher mass means slower response but more stability.
* **Stiffness (0.1-50)**: Controls how strongly vertices return to their original position. Higher values result in more rigid behavior.
* **Damping (0.1-30)**: Controls how quickly oscillations settle down. Higher values result in quicker stabilization.

### Distance Settings

* **Maintain Radius**: When enabled, vertices maintain their distance from the pivot point, preventing the mesh from collapsing or expanding unnaturally.
* **Distance Falloff (0.01-10)**: Controls how distance affects the jelly physics. Higher values reduce the effect for distant vertices.
* **Radius Constraint Strength (0-1)**: Controls how strictly vertices maintain their distance from the pivot. Higher values enforce stricter distance constraints.

### Response Settings

* **Movement Influence (0.1-10)**: Controls how strongly the mesh responds to object movement and rotation.
* **Falloff (0.01-10)**: Controls the radius of influence for physics effects. Vertices beyond this distance are less affected.

### Collider Settings

* **Update Mesh Collider**: When enabled, the mesh collider will update to match deformations (requires a MeshCollider component).
* **Collider Update Interval (1-20)**: Controls how often the collider updates. Higher values improve performance but reduce collision accuracy.

### Performance Settings

* **Performance Level (0-1)**: Adjust this slider to balance between quality and performance.
  * 0 = Highest quality (updates every frame)
  * 1 = Highest performance (reduced update frequency)

### LOD Settings

* **Use LOD**: Enables automatic Level of Detail based on camera distance.
* **Min LOD Distance (1-50)**: Distance below which full quality is used.
* **Max LOD Distance (10-200)**: Distance at which the effect is completely disabled for maximum performance. When objects are beyond this distance, the mesh will automatically return to its original shape.
* **Show Gizmos**: Toggles the visualization of LOD ranges and pivot points in the Scene view.

## Tips and Best Practices

### Performance Optimization

1. **Start with lower complexity meshes**: The more vertices in your mesh, the more computational power required
2. **Use the Performance Level slider**: For distant or less important objects, increase the performance level
3. **Enable LOD for objects in large scenes**: This automatically adjusts performance based on camera distance and completely disables the effect beyond the Max LOD Distance
4. **Adjust the Max LOD Distance**: Set this to an appropriate value for your scene - objects beyond this distance will have the effect disabled completely
5. **Limit Mesh Collider updates**: Only enable this when necessary for gameplay


### Realistic Jelly Physics

1. **Use appropriate mass values**: Objects with different sizes should have proportional mass values
2. **Adjust stiffness based on material**: Harder materials should have higher stiffness values:
   * Soft jelly: 5-10
   * Rubber: 15-25
   * Firm but elastic: 30-40
3. **Balance damping with the effect you want**: 
   * Wobbly, long-lasting effect: 0.1-0.3
   * Moderate jiggle: 0.4-0.7
   * Quick stabilization: 0.8-1.0
4. **Use custom pivots for complex shapes**: Position the pivot at the natural center of mass

### Common Use Cases

- **Character physics**: Add secondary motion to character parts like hair, clothing, fat
- **Environmental objects**: Make plants, fungi, or alien structures react to player interaction
- **Food and consumables**: Create realistic jelly, slimes, or soft food items
- **Vehicles**: Add suspension and wheel deformation for more realistic vehicles
- **Interactive UI**: Create playful, physically-reactive UI elements

## Troubleshooting

### Common Issues and Solutions

#### "Mesh is not readable" Error
* Make sure to enable "Read/Write Enabled" in your mesh's import settings

#### Performance Problems
* Reduce the number of vertices in your mesh
* Increase the Performance Level slider
* Enable LOD for distant objects
* Reduce the Collider Update Interval

#### Unstable or Extreme Deformation
* Decrease Intensity
* Increase Mass
* Increase Stiffness
* Enable Maintain Radius option

#### Mesh Collapses or Expands Unnaturally
* Enable Maintain Radius
* Increase Radius Constraint Strength
* Check that your custom pivot is correctly positioned

#### No Visible Effect
* Increase Intensity
* Decrease Stiffness
* Verify the object is moving or rotating to trigger the effect

## API Reference

### Public Methods

* **ResetMesh()**: Resets the mesh to its original undeformed state
* **CheckPivotChange()**: Called automatically to check if the custom pivot has moved. You don't need to call this directly.

### Important Properties

All the properties visible in the Inspector can be modified at runtime through code:

```csharp
// Example: Dynamically adjust jelly properties based on game state
public JellyMeshSystem jellyMesh;

void MakeMoreJiggly()
{
    jellyMesh.intensity = 15f;
    jellyMesh.stiffness = 10f;
    jellyMesh.damping = 0.3f;
}

void MakeMoreStable()
{
    jellyMesh.intensity = 5f;
    jellyMesh.stiffness = 40f;
    jellyMesh.damping = 0.8f;
}
```

## Requirements

* Unity 2019.3 or higher
* Mesh with Read/Write Enabled option checked
* Burst. Jobs, Mathematics, Colldecitons packages installed

## About the JellyMesh System

The JellyMesh System utilizes Unity's Jobs system and the Burst compiler to achieve high-performance soft-body physics. 

## Author 

https://github.com/roundyyy

Was it useful? Maybe get me some coffee :)
https://ko-fi.com/roundy