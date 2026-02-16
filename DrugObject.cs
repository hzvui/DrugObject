using System.Collections;
using System.Collections.Generic;
using Unigine;
using static Unigine.Input;

[Component(PropertyGuid = "11b271e3bd5f3617937b461bd6d95673c5c9d8b0")]
public class DrugObject : Component
{


    [ShowInEditor]
    public Material cloneMaterial = null;


    private Node selectedObject;
    private mat4 transform;
    private Dictionary<Node, (mat4, vec3)> initialTransforms = new();
    public static Dictionary<Node, mat4> initialInitTransforms = new();
    private float distanceFromController;
    private const float minDistance = 0.1f;
    private const float maxDistance = 5.0f;
    private const float distanceSpeed = 2.0f;
        [ShowInEditor]
[ParameterSlider(Title = "Snap Distance", Group = "VR Transform Movable Object", Min = 0.01f, Max = 1.0f)]
public float snapDistance = 0.2f; // по умолчанию 20 см

    private InputVRController rightController;
    private InputVRController leftController;
    private Node rightControllerNode;
    private Node leftControllerNode;

    private float baseDistance = 1.0f;
    private float scaleFactor = 3f;
    private bool first = true;
    private bool MaxRotating = false;
    private vec3 initialScale;
    private vec3 scale;
    private vec3 finishScale;

    private dvec3 lastLeftControllerPos; // Последняя позиция левого контроллера
    private bool isRotating = false;     // Флаг вращения

    dmat4 fromOrigin;
    dmat4 rotation;
    dmat4 toOrigin;

    public static Node myNode = null;

    public Dictionary<int, Object> cloneModels = new();
    private GetterModels getterModels;

    private WidgetMenuUIHand menuUIHand;
    private PCWidgetMenuUIHand PCmenuUIHand;

    [ShowInEditor] [ParameterSlider(Title = "Мир изучения", Group = "Миры")] private bool isLearningWorld;
    [ShowInEditor] [ParameterSlider(Title = "Мир последовательной сборки", Group = "Миры")] private bool isAssemblyWorld;
    [ShowInEditor] [ParameterSlider(Title = "Мир экзамена", Group = "Миры")] private bool isExamWorld;

    private AssemblyExam assemblyExam;
    private VRMenuSample vRMenu;
    
    void Init()
    {
        
        rightController = Input.VRControllerRight;
        leftController = Input.VRControllerLeft;

        menuUIHand = FindComponentInWorld<WidgetMenuUIHand>();
        
        PCmenuUIHand = FindComponentInWorld<PCWidgetMenuUIHand>();
        assemblyExam = FindComponentInWorld<AssemblyExam>();

        selectedObject = null;
        CameraCast.SetDrugged(false);

        rightControllerNode = World.GetNodeByName("right_controller");
        leftControllerNode = World.GetNodeByName("left_controller");

        Visualizer.Enabled = true;
        getterModels = FindComponentInWorld<GetterModels>();
        myNode = getterModels.GetNode();
        AddFullInitialTransform(myNode);


    }

    // public void GetNode()
    // {
    //     //myNode = World.GetNodeByID(ImportModel.ModelId);
    //     myNode = getterModels.GetNode();
    // }



    public static void AddFullInitialTransform(Node currentNode)
    {
        if (currentNode == null) return;

        if (!initialInitTransforms.ContainsKey(currentNode))
        {
            initialInitTransforms.Add(currentNode, currentNode.WorldTransform);
            Log.Message($"Stored initial transform for {currentNode.Name}\n");
        }

        for (int i = 0; i < currentNode.NumChildren; i++)
        {
            AddFullInitialTransform(currentNode.GetChild(i));
        }
    }

    void Update()
    {
        if (vRMenu = null)
            vRMenu = FindComponentInWorld<VRMenuSample>();
        if (CameraCast.GetDrugged() && selectedObject != null)
        {
            if (rightController != null && leftController != null)
            {
                if (leftController.IsButtonPressed(Input.VR_BUTTON.GRIP) &&
                   rightController.IsButtonPressed(Input.VR_BUTTON.GRIP) &&
                   !MaxRotating)
                {
                    ChangeScale();
                }

                if (leftController.IsButtonPressed(Input.VR_BUTTON.X) &&
                rightController.IsButtonPressed(Input.VR_BUTTON.GRIP) &&
                !MaxRotating)
                {
                    selectedObject.Rotate(0, 1, 0);
                    MaxRotating = true;

                }
                else
                {
                    isRotating = false; // Сбрасываем флаг, если триггер отпущен
                    MaxRotating = false;
                }
            }
            else
            {
                if (Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.LEFT) &&
                Input.IsKeyPressed(Input.KEY.L))
                {
                    ChangeScale();
                }

                if (Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.LEFT) &&
                Input.IsKeyPressed(Input.KEY.X))
                {
                    selectedObject.Rotate(0, 1, 0);
                    MaxRotating = true;

                }
                else
                {
                    isRotating = false; // Сбрасываем флаг, если триггер отпущен
                    MaxRotating = false;
                }
            }
            if (!MaxRotating)
            {
                HandleGrabbing();
                if (rightController != null)
                    UpdateTransform();
                else
                    UpdateTransformPC();                
            }

        }
        else
        {
            if (!MaxRotating)
                SelectAndGrabObject();
        }

        if (rightController != null)
        {
            if (leftController.IsButtonUp(Input.VR_BUTTON.GRIP) ||
                rightController.IsButtonUp(Input.VR_BUTTON.GRIP) && leftController != null && rightController != null)
            {
                first = true;
            }
        }

    }

    private void RotateObject()
    {
        dvec3 currentLeftControllerPos = leftControllerNode.WorldPosition;

        if (!isRotating)
        {
            // Инициализация при первом нажатии триггера
            lastLeftControllerPos = currentLeftControllerPos;
            isRotating = true;
            return;
        }

        // Вычисляем центр объекта (BoundBox)
        BoundBox bb = selectedObject.BoundBox;
        dvec3 objectCenter = (bb.maximum + bb.minimum) * 0.5f;

        // Вычисляем изменение позиции контроллера
        dvec3 deltaPos = currentLeftControllerPos - lastLeftControllerPos;

        // Вычисляем углы вращения (в радианах) на основе перемещения
        float rotationSpeed = 2.0f; // Чувствительность вращения
        double angleX = deltaPos.y * rotationSpeed; // Вращение вокруг X (вертикальное движение)
        double angleY = deltaPos.x * rotationSpeed; // Вращение вокруг Y (горизонтальное движение)

        // Получаем текущую трансформацию объекта
        dmat4 currentTransform = selectedObject.WorldTransform;

        // Создаем матрицы вращения
        dmat4 rotationX = MathLib.RotateX(angleX);
        dmat4 rotationY = MathLib.RotateY(angleY);
        rotation = rotationY * rotationX;

        // Смещаем объект к началу координат (относительно центра)
        toOrigin = MathLib.Translate(-objectCenter);
        fromOrigin = MathLib.Translate(objectCenter);

        // Обновляем последнюю позицию контроллера
        lastLeftControllerPos = currentLeftControllerPos;

        Log.Message($"Rotating object: X:{angleX}, Y:{angleY}\n");
    }

    private void ChangeScale()
    {
        if (first)
        {
            initialScale = selectedObject.Scale;
            baseDistance = GetDistanceByControllers();
            Log.Message($"Base distance: {baseDistance}\n");
            first = false;
        }
        float distance = GetDistanceByControllers() - baseDistance;
        float scaling = distance * scaleFactor;
        scale = new(scaling, scaling, scaling);
        // selectedObject.Scale = initialScale + scale;
        finishScale = initialScale + scale;
    }

    private float GetDistanceByControllers()
    {
        dvec3 leftControllerPos = leftControllerNode.WorldPosition;
        dvec3 rightControllerPos = rightControllerNode.WorldPosition;
        dvec3 distanceVector = rightControllerPos - leftControllerPos;
        float distance = MathLib.Abs((float)distanceVector.Length);
        return distance;
    }

    private void UpdateTransform()
    {
        mat4 controllerTransform = rightController.GetWorldTransform();
        mat4 inverseTransform = MathLib.Inverse(controllerTransform);

        if (selectedObject == null) return;
        transform = inverseTransform * selectedObject.WorldTransform;
    }

    private void UpdateTransformPC() // может быть но я не уверен
    {
        //mat4 controllerTransform = CameraCast.GetIWorldTransform();
        //mat4 inverseTransform = MathLib.Inverse(controllerTransform);

        if (selectedObject == null) return;
        //transform = inverseTransform * selectedObject.WorldTransform;
        transform = CameraCast.GetIWorldTransform() * selectedObject.WorldTransform;
    }
    
    // вот короче если я нажимаю на Грип то у меня это вызывается. Надо чтобы если я нажимаю на ПКМ или ЛКМ(если if(rightController!=null) было тоже самое) 206 стр

    private void SelectAndGrabObject()
    {
        selectedObject = CameraCast.GetObject();

        if (myNode != null && CameraCast.movingAllObject && selectedObject.RootNode.Name == myNode.Name)
        {
            selectedObject = myNode;
        }

        if (selectedObject == null || selectedObject.RootNode.Name == "static_content" || selectedObject.RootNode.Name == "toolsNode") return;
        if (rightController != null)
        {
            if (rightController.IsButtonDown(Input.VR_BUTTON.GRIP) && !CameraCast.GetDrugged())
            {
                finishScale = selectedObject.Scale;
                CameraCast.SetDrugged(true); 
                AddInitialTransform(selectedObject);

                UpdateTransform(); // сделал на ПК
                distanceFromController = (float)(selectedObject.WorldPosition - rightController.GetWorldTransform().GetColumn3(3)).Length;
                Log.Message($"Grabbing object: {selectedObject.Name} with initial transform stored\n");

                Indicators.mainObject = selectedObject;

                if(isLearningWorld)
                {            
                    if (menuUIHand != null)
                    {
                        menuUIHand.SetDetailName(selectedObject.Name);
                        menuUIHand.SetDetailInformation(selectedObject);
                        
                    }
                    if (PCmenuUIHand != null)
                    {
                        PCmenuUIHand.SetDetailName(selectedObject.Name);
                        PCmenuUIHand.SetDetailInformation(selectedObject);
                    }

                    if (cloneModels.TryGetValue(selectedObject.ID, out var cloneModel) &&
                        selectedObject.Name != "TreeGui" &&
                        selectedObject.Name != myNode.Name)
                    {
                        cloneModel.Enabled = true;
                    }
                    else if (selectedObject.Name != "TreeGui" &&
                        selectedObject.Name != myNode.Name)
                    {
                        cloneModel = selectedObject.Clone() as Object;
                        cloneModel.SetMaterial(cloneMaterial, 0);
                        cloneModel.SetIntersection(false, 0);
                        cloneModels.Add(selectedObject.ID, cloneModel);
                    }
                }

                if(isExamWorld)
                {            
                    if (menuUIHand != null)
                    {
                        menuUIHand.SetDetailName(selectedObject.Name);
                        menuUIHand.SetDetailInformation(selectedObject);
                        
                    }
                    if (PCmenuUIHand != null)
                    {
                        PCmenuUIHand.SetDetailName(selectedObject.Name);
                        PCmenuUIHand.SetDetailInformation(selectedObject);
                    }

                    if (cloneModels.TryGetValue(selectedObject.ID, out var cloneModel) &&
                        selectedObject.Name != "TreeGui" &&
                        selectedObject.Name != myNode.Name)
                    {
                        cloneModel.Enabled = true;
                    }
                    else if (selectedObject.Name != "TreeGui" &&
                        selectedObject.Name != myNode.Name)
                    {
                        cloneModel = selectedObject.Clone() as Object;
                        assemblyExam.SetPosToClone(cloneModel, selectedObject);
                        cloneModel.SetMaterial(cloneMaterial, 0);
                        cloneModel.SetIntersection(false, 0);
                        cloneModels.Add(selectedObject.ID, cloneModel);
                    }
                }
            }
        }
        else
        {
            if (Input.IsMouseButtonPressed(Input.MOUSE_BUTTON.LEFT) && !CameraCast.GetDrugged())
            {
                finishScale = selectedObject.Scale;
                CameraCast.SetDrugged(true); 
                AddInitialTransform(selectedObject);

                UpdateTransformPC(); // сделал на ПК
                distanceFromController = (float)(selectedObject.WorldPosition - CameraCast.GetIWorldTransform().GetColumn3(3)).Length;//тоже изменить под ПК
                Log.Message($"Grabbing object: {selectedObject.Name} with initial transform stored\n");
                Log.MessageLine(Unigine.World.IsLoaded);

                Indicators.mainObject = selectedObject;

                if(isLearningWorld)
                {
                    if (PCmenuUIHand != null)
                    {
                        PCmenuUIHand.SetDetailName(selectedObject.Name);
                        PCmenuUIHand.SetDetailInformation(selectedObject);
                    }
                    if (cloneModels.TryGetValue(selectedObject.ID, out var cloneModel) &&
                        selectedObject.Name != "TreeGui" &&
                        selectedObject.Name != myNode.Name)
                    {
                        cloneModel.Enabled = true;
                    }
                    else if (selectedObject.Name != "TreeGui" &&
                        selectedObject.Name != myNode.Name)
                    {
                        cloneModel = selectedObject.Clone() as Object;
                        cloneModel.SetMaterial(cloneMaterial, 0);
                        cloneModel.SetIntersection(false, 0);
                        cloneModels.Add(selectedObject.ID, cloneModel);
                    }                    
                }
                if (isExamWorld)
                {
                    if (PCmenuUIHand != null)
                    {
                        PCmenuUIHand.SetDetailName(selectedObject.Name);
                        PCmenuUIHand.SetDetailInformation(selectedObject);
                    }
                    if (cloneModels.TryGetValue(selectedObject.ID, out var cloneModel) &&
                        selectedObject.Name != "TreeGui" &&
                        selectedObject.Name != myNode.Name)
                    {
                        cloneModel.Enabled = true;
                    }
                    else if (selectedObject.Name != "TreeGui" &&
                        selectedObject.Name != myNode.Name)
                    {

                        cloneModel = selectedObject.Clone() as Object;
                        assemblyExam.SetPosToClone(cloneModel, selectedObject);
                        cloneModel.SetMaterial(cloneMaterial, 0);
                        cloneModel.SetIntersection(false, 0);
                        cloneModels.Add(selectedObject.ID, cloneModel);
                    }                          
                }
            }
        }
    }


    private void HandleGrabbing()
    {
        if(rightController!=null)
        {           
            if (rightController.IsButtonUp(Input.VR_BUTTON.GRIP))
            {
                SetToInitialTransform();
                CameraCast.SetDrugged(false);

                Indicators.initTransform = true;
                Indicators.pastObject = selectedObject;
                Indicators.SetTransform(false);
                Indicators.mainObject = null;

                if (cloneModels.TryGetValue(selectedObject.ID, out var cloneModel))
                {
                    cloneModel.Enabled = false;
                }

                selectedObject = null;
                Log.Message("Object released\n");
                return;
            }

            vec3 controllerPos = rightController.GetWorldTransform().GetColumn3(3);
            vec3 controllerDir = rightController.GetWorldTransform().GetColumn3(2);

            selectedObject.WorldTransform = CameraCast.GetOldWorldTransform() * transform;
            selectedObject.Scale = finishScale;
            Indicators.SetTransform(true);
            float dist = rightController.GetAxis(1);

            if (dist < -0.5f)
            {
                selectedObject.WorldPosition += controllerDir.Normalized * 0.05f;
            }

            if (dist > 0.5f)
            {
                selectedObject.WorldPosition -= controllerDir.Normalized * 0.05f;
            }
            Log.Message($"Select Object Drag Scale {selectedObject.WorldScale}\n");
        }
        else
        {
            if (Input.IsMouseButtonUp(Input.MOUSE_BUTTON.LEFT))
            {
                SetToInitialTransform();
                CameraCast.SetDrugged(false);

                Indicators.initTransform = true;
                Indicators.pastObject = selectedObject;
                Indicators.SetTransform(false);
                Indicators.mainObject = null;

                if ((isLearningWorld || isExamWorld) && cloneModels.TryGetValue(selectedObject.ID, out var cloneModel))
                {
                    cloneModel.Enabled = false;
                }

                selectedObject = null;
                Log.Message("Object released\n");
                return;
            }

            vec3 controllerPos = CameraCast.GetIWorldTransform().GetColumn3(3);
            vec3 controllerDir = CameraCast.GetIWorldTransform().GetColumn3(2);

            selectedObject.WorldTransform = CameraCast.GetOldWorldTransform() * transform;
            selectedObject.Scale = finishScale;
            Indicators.SetTransform(true);
            
            // Убираем ненужные манипуляции с расстоянием до клона во время перемещения
            // Оставляем только перемещение объекта за курсором
            
            Log.Message($"Select Object Drag Scale {selectedObject.WorldScale}\n");            
        }
    }

    private void SetToInitialTransform()
    {
        if(isLearningWorld)
        {
            if (initialTransforms.TryGetValue(selectedObject, out var initialValues) &&
                CameraCast.movingAllObject == false && (isLearningWorld || isExamWorld))
            {
                double distance = (selectedObject.WorldBoundBox.Center - initialValues.Item2).Length;
                if (distance < snapDistance)
                {
                    selectedObject.WorldTransform = initialValues.Item1;

                    if (cloneModels.TryGetValue(selectedObject.ID, out var cloneModel) && cloneModel != null)
                    {
                        cloneModel.Enabled = false;
                    }
                    Log.Message($"Snapped {selectedObject.Name} back to initial transform\n");
                }
            }            
        }


            // Режим экзамена: магнитим к позиции КЛОНА
        if(isExamWorld)
        {
            if (cloneModels.TryGetValue(selectedObject.ID, out var cloneModel) && cloneModel != null)
            {
                dvec3 cloneCenter = cloneModel.WorldBoundBox.Center;
                dvec3 currentCenter = selectedObject.WorldBoundBox.Center;
                double distance = (currentCenter - cloneCenter).Length;

                if (distance < snapDistance)
                {
                    // Приводим объект точно к трансформу клона
                    selectedObject.WorldTransform = cloneModel.WorldTransform;

                    cloneModel.Enabled = false; // скрываем клон после "установки"
                    Log.Message($"Snapped {selectedObject.Name} to clone position in exam mode\n");
                }
            }            
        }
    }

    private void AddInitialTransform(Node obj)
    {
        if (obj.Name == "TreeGui" || obj.RootNode == null) return;

        if (!initialTransforms.ContainsKey(obj))
        {
            initialTransforms.Add(obj, (obj.WorldTransform, obj.WorldBoundBox.Center));
            Log.Message($"Stored initial transform for {obj.Name}\n");
        }
    }

    public void AllSetInitialTransform()
    {

        foreach (var item in initialTransforms)
        {
            item.Key.WorldTransform = item.Value.Item1;
            cloneModels.TryGetValue(item.Key.ID, out var cloneModel);
            if (cloneModel != null)
            {
                cloneModels.Remove(item.Key.ID);
            }
        }
        // initialTransforms.Clear();
        cloneModels.Clear();
        initialTransforms.Clear();
    }
    
    public void AllFullInitialTransform()
    {
        foreach (var item in initialInitTransforms)
        {
            item.Key.WorldTransform = item.Value;
            cloneModels.TryGetValue(item.Key.ID, out var cloneModel);
            if (cloneModel != null)
            {
                cloneModels.Remove(item.Key.ID);
            }
        }
        cloneModels.Clear();
        initialTransforms.Clear();
    }


}