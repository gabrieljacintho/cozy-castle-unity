---
trigger: always_on
---
# AI Global Rules

# Context
*   Game development
*   Unity 6 URP Deferred
*   C# programming language
*   Root Namespace: GabrielBertasso
*   Target Platforms: PC
*   Graphics: 3D
*   Never assume anything, always ask me.
# Code Style Guide
## Naming Rules
*   Types and namespaces: UpperCamelCase
*   Interfaces: IUpperCamelCase
*   Type parameters: TUpperCamelCase
*   Methods: UpperCamelCase
*   Properties: UpperCamelCase
*   Events: UpperCamelCase
*   Local variables: lowerCamelCase
*   Parameters: lowerCamelCase
*   Fields (public): UpperCamelCase
*   Fields (private/protected): _lowerCamelCase
*   Static fields: s_lowerCamelCase
*   Constant fields: UpperCamelCase
*   Enum members: UpperCamelCase
*   All other entities: UpperCamelCase
## Fields and Variables
*   Use nouns for variable names.
*   Prefix Booleans with a verb: e.g. isDead, isWalking, hasDamageMultiplier, etc.
*   Use meaningful names. Don’t abbreviate (unless it’s math).
*   DO NOT omit access level in fields.
*   Always write only one variable declaration per line and place all the variable's attributes on the same line.
*   Avoid redundant names: If your class is called Player, you don’t need to create member variables called PlayerScore or PlayerTarget. Trim them down to Score or Target.
*   Use the var keyword for implicitly typed local variables if it helps readability and the type is obvious.
## Events and Event Handlers
*   Use UnityEvent for events that need Inspector configuration or designer-friendly setup.
*   For performance-critical scenarios (called every frame or in tight loops), consider using C# native events (event Action/EventHandler) instead of UnityEvent.
*   Name the event with a verb phrase: For example, specify "OnOpeningDoor" for an event before opening a door or "OnDoorOpened" for an event afterward.
*   Prefix the event raising method (in the subject) with "On": e.g. "OnOpeningDoor" or "OnDoorOpened."
*   Unsubscribe from events in the OnDestroy or OnDisable functions.
*   Never add SerializeField attribute to events, make them public instead.
## Formatting
*   Use the SerializeField attribute with private access instead of public access.
*   Use the Header and Tooltip attributes for Inspector organization.
*   When values ​​outside a range don't make sense, use the Range attribute to define minimum and maximum values.
*   Add the RequireComponent attribute when needed.
*   Always group all attributes within a single pair of square brackets.
*   Group data in serializable classes or structs to clean up the Inspector: Define a public class or struct and mark it with the \[Serializable\] attribute. Define public variables for each type you want to expose in the Inspector.
*   NEVER omit braces, even for single-line statements.
*   Always write each brace on a separate line.
*   Always add a blank line after a closing brace.
*   Keep lines short. Consider horizontal whitespace: Decide on a standard line width (80–120 characters). Break a long line into smaller statements rather than letting it overflow.
*   Maintain indentation/hierarchy: Indent your code to increase legibility.
*   Don’t use column alignment unless needed for readability
*   Group dependent and/or similar methods together: Code needs to be logical and coherent. Keep methods that do the same thing next to one another, so someone reading your logic doesn’t have to jump around the file.
## Regions
*   Use regions only when necessary to improve code readability.
*   If the code is too long, consider splitting it into more classes/code files instead of creating regions.
## Class Organization
*   Prefer composition over inheritance.
*   Always write the elements of each class or struct in the following order:
    1. Public/serialized fields
    2. Protected fields
    3. Private fields
    4. Public properties
    5. Protected properties
    6. Private properties
    7. Public events (of any type, e.g. UnityEvent)
    8. Protected events (of any type, e.g. UnityEvent)
    9. Private events (of any type, e.g. UnityEvent)
    10. MonoBehaviour methods in order of execution (e.g. Awake, OnEnable, Start, Update, etc.)
    11. Public methods
    12. Protected methods
    13. Private methods
*   Never comment on the order above.
*   Never create empty MonoBehaviour methods.
*   Recall the recommended class naming rules in Unity: The source file name must match the name of the Monobehaviour in the file. You might have other internal classes in the file, but only one Monobehaviour should exist per file.
*   Use the OnValidate function for inspector field validation.
## Methods
*   Methods returning bool should ask questions: e.g., IsGameOver, HasStartedTurn.
*   Use fewer arguments: Arguments can increase the complexity of your method. Reduce their number to make your methods easier to read and test.
*   Avoid excessive overloading
*   Avoid side effects: A method only needs to do what its name advertises. Avoid modifying anything outside of its scope.
*   Instead of passing in a flag, make another method: Don’t set up your method to work in two different modes based on a flag. Make two methods with distinct names.
*   Avoid large, complex methods; break them down into smaller ones.
## Namespace
*   Considering broad grouping, whenever code can be grouped with others, place them in a subnamespace with an appropriate name, and move all code files to a subfolder with the same name as the subnamespace. But avoid creating more than two nested subfolders. For example: A script called PlayerController should be created in the Assets/Scripts/Player folder with the namespace Timedrift.Player
*   Never ask about namespaces, always choose them yourself.
## Comments
Here are some dos and don’ts for comments:
*   Use a tooltip instead of a comment for serialized fields.
*   Place the comment on a separate line when possible, not at the end of a line of code.
*   Always avoid writing comments or tooltips. Only write them when they are absolutely relevant.
## Odin Inspector
*   Use the ShowIf and HideIf attributes to dynamically display only useful variables in the inspector.
*   Use the Button attribute on functions that may be useful to call from the inspector for testing.
*   Use the ShowInInspector and ReadOnly attributes on variables that may be useful to view in the inspector but should not be changed through the inspector.
*   If necessary, check the Odin Inspector documentation here: https://odininspector.com/documentation
# Unity Specifications
*   Always use New Input System with InputActionReference instead of the Old Input System.
*   Never write code to enable or disable InputActions unless I ask you to.
*   Never call the DontDestroyOnLoad method unless I ask you to.
*   Create ScriptableObjects with CreateAssetMenu attribute for data.
*   Use async/await with CancellationTokens for non-Unity operations.
*   Use coroutines for Unity-specific timing (WaitForEndOfFrame, etc.).
*   Never create .meta files.
# Performance
*   Never use GetComponent in the Update/FixedUpdate/LateUpdate functions.
*   Cache functions like WaitForSeconds: e.g. private static readonly WaitForSeconds WaitOneSecond = new WaitForSeconds(1f);
*   Use the TryGetComponent function when component might not exist.
*   Use the CompareTag function instead of gameObject.tag ==
# Code Generation
*   Always include proper using statements.
*   Include error handling and null checks.
*   Use modern C# features (null-conditional operators, expression bodies).
*   Follow SOLID principles adapted for Unity's component system.
*   The year is 2026, make sure we apply the proper year for searches and updates.
*   Always write everything in English.
*   Always avoid duplicate code and logic.
*   Always keep your code clean and well-organized.
*   Ensure that the code architecture is always well-designed.
*   Before implementing any tasks, always read all relevant files completely.
# Fish-Net
*   Use FishNet v4 to implement online multiplayer.
*   Always consider client-host architecture.
*   Always use SyncVar<> instead of \[SyncVar\].