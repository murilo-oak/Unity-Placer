# Unity Placer

![Unity Placer Banner](public/Screenshot.png)

## Description 📝

The "Unity Placer" repository contains a script that allows the user to access the "Assets/Prefabs" folder within a Unity project and view all the models present in that folder. The main functionality of this script is the "Placer" tool, which is accessible through a new tab called "Tools" in the upper corner of the Unity interface.

This tool was based on the assignment from the course "Intro to Tool Dev in Unity," which is part of the following playlist: [Intro to Tool Dev in Unity Playlist](https://www.youtube.com/playlist?list=PLImQaTpSAdsBKEkUvKxw6p0tpwl7ylw0d).

## Functionality 🛠️

With the "Placer" tool, the user can select the quantity of assets to be instantiated and define an area using a radius where these assets will be placed in the environment. The radius determines the size of the circular area where the objects will be distributed.

## Collision Detection 🚫

If an object's height is such that it collides with any object above it, the object turns red and is not instantiated to avoid overlapping or clipping issues.

## Instantiation ⚡️

To instantiate the selected objects in the scene camera, simply toggle the desired objects and press the spacebar. This will create the objects within the defined area determined by the radius, taking into account collision detection.

## Installation 🔧

1. Clone the Unity Placer repository to your preferred location:

   ```
   git clone https://github.com/your-username/unity-placer.git
   ```

2. Open the Unity Hub and add the "unity-placer" project by clicking "Add" and selecting the cloned folder.

3. Select the "unity-placer" project in the Unity Hub and wait for Unity to load the project.

4. Once the project is open in Unity, navigate to the "Assets/Scripts" folder and locate the "Placer.cs" script.

5. Drag and drop the "Placer.cs" script onto the main camera object in your scene.

6. Now you can access the "Tools" tab in the upper corner of the Unity interface to use the "Placer" tool.

## Usage 🚀

1. Open Unity and load the "unity-placer" project.

2. In the Unity interface, click on the "Tools" tab in the upper corner.

3. Select the "Placer" tool in the "Tools" tab.

4. Set the quantity of assets to be instantiated and adjust the radius using the mouse scroll to increase or decrease the value.

5. If you want to use the mouse scroll to zoom in the scene, press the "Alt" key.

6. Toggle the desired objects using the toggle feature.

7. Press the spacebar to instantiate the selected objects within the defined area determined by the radius, ensuring collision detection to avoid overlapping or clipping.

8. To undo the instantiation action, press the "Control + Z" keys.

## License 📄

This project is licensed under the [MIT License](LICENSE).