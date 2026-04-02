grammar HtnScript

entry Model:
    role=RoleDecl;

RoleDecl:
    'role' name=ID '{'
        'slots' '{' slots+=SlotDecl (',' slots+=SlotDecl)* '}'
        rules+=RuleDecl*
    '}';

SlotDecl:
    name=ID;

RuleDecl:
    'rule' name=ID '{'
        'when' condition=Condition
        ('interrupt_when' interruptCondition=Condition)?
        statement=RuleStatement
    '}';

RuleStatement:
    DoStatement | ChooseStatement;

DoStatement:
    {infer DoStatement} 'do' statement=ExecutableStatement;

ExecutableStatement:
    ActionStatement | SequenceStatement;

ChooseStatement:
    {infer ChooseStatement} 'choose' '{' options+=ChoiceOption+ '}';

ChoiceOption:
    chance=ChanceGate? statement=ExecutableStatement;

ChanceGate:
    'chance' percent=INT '->';

SequenceStatement:
    {infer SequenceStatement} 'sequence' '{' actions+=ActionStatement+ '}';

ActionStatement:
    {infer ActionStatement} action=Action ';';

Action:
    ChaseAction | MoveToAction | GoHomeAction | JumpAction | SpinAction | WaitAction | PickRockAction | ThrowAtAction | EatNearestShroomAction | StartleAction | FleeAction | WanderAction;

ChaseAction:
    {infer ChaseAction} 'chase' '(' target=[SlotDecl:ID] ')';

MoveToAction:
    {infer MoveToAction} 'move_to' '(' target=TargetExpr ')';

GoHomeAction:
    {infer GoHomeAction} 'go_home';

JumpAction:
    {infer JumpAction} 'jump';

SpinAction:
    {infer SpinAction} 'spin';

WaitAction:
    {infer WaitAction} 'wait';

PickRockAction:
    {infer PickRockAction} 'pick_rock';

ThrowAtAction:
    {infer ThrowAtAction} 'throw_at' '(' target=[SlotDecl:ID] ')';

EatNearestShroomAction:
    {infer EatNearestShroomAction} 'eat_nearest_shroom';

StartleAction:
    {infer StartleAction} 'startle' '(' target=[SlotDecl:ID] ')';

FleeAction:
    {infer FleeAction} 'flee' '(' target=[SlotDecl:ID] ')';

WanderAction:
    {infer WanderAction} 'wander' '(' target=[SlotDecl:ID] ')';

TargetExpr:
    SlotTarget | NearestTarget;

SlotTarget:
    {infer SlotTarget} slot=[SlotDecl:ID];

NearestTarget:
    {infer NearestTarget} 'nearest' '(' kind=KindLiteral ')';

Condition:
    {infer Condition} disjuncts+=Conjunction ('or' disjuncts+=Conjunction)*;

Conjunction:
    {infer Conjunction} operands+=PrimaryCondition ('and' operands+=PrimaryCondition)*;

PrimaryCondition:
    NegatedCondition | AtomicCondition;

NegatedCondition:
    {infer NegatedCondition} 'not' operand=PrimaryCondition;

AtomicCondition:
    ParenthesizedCondition | Cue;

ParenthesizedCondition:
    {infer ParenthesizedCondition} '(' condition=Condition ')';

Cue:
    IdleCue | TreasureTakenCue | SeesCue | AtCue | HasCue | ExistsCue;

IdleCue:
    {infer IdleCue} 'idle';

TreasureTakenCue:
    {infer TreasureTakenCue} 'treasure_taken';

SeesCue:
    {infer SeesCue} 'sees' '(' slot=[SlotDecl:ID] ')';

AtCue:
    {infer AtCue} 'at' '(' slot=[SlotDecl:ID] ')';

HasCue:
    {infer HasCue} 'has' '(' item=ItemLiteral ')';

ExistsCue:
    {infer ExistsCue} 'exists' '(' kind=KindLiteral ')';

ItemLiteral returns string:
    'rock';

KindLiteral returns string:
    'rock' | 'shroom';

hidden terminal WS: /\s+/;
terminal ID: /[_a-zA-Z][\w_]*/;
terminal INT returns number: /[0-9]+/;

hidden terminal ML_COMMENT: /\/\*[\s\S]*?\*\//;
hidden terminal SL_COMMENT: /\/\/[^\n\r]*/;
