import type { ValidationAcceptor, ValidationChecks } from 'langium';
import type { ChoiceOption, HtnScriptAstType, RoleDecl } from './generated/ast.js';
import type { HtnScriptServices } from './htn-script-module.js';

/**
 * Register custom validation checks.
 */
export function registerValidationChecks(services: HtnScriptServices) {
    const registry = services.validation.ValidationRegistry;
    const validator = services.validation.HtnScriptValidator;
    const checks: ValidationChecks<HtnScriptAstType> = {
        RoleDecl: validator.checkRoleIntegrity,
        ChoiceOption: validator.checkChanceRange
    };
    registry.register(checks, validator);
}

/**
 * Implementation of custom validations.
 */
export class HtnScriptValidator {

    checkRoleIntegrity(role: RoleDecl, accept: ValidationAcceptor): void {
        const seenSlots = new Set<string>();
        for (const slot of role.slots) {
            if (seenSlots.has(slot.name)) {
                accept('error', `Duplicate slot '${slot.name}'.`, { node: slot, property: 'name' });
            } else {
                seenSlots.add(slot.name);
            }
        }

        const seenRules = new Set<string>();
        for (const rule of role.rules) {
            if (seenRules.has(rule.name)) {
                accept('error', `Duplicate rule '${rule.name}'.`, { node: rule, property: 'name' });
            } else {
                seenRules.add(rule.name);
            }
        }
    }

    checkChanceRange(option: ChoiceOption, accept: ValidationAcceptor): void {
        if (option.chance && (option.chance.percent < 0 || option.chance.percent > 100)) {
            accept('error', 'Chance must be between 0 and 100.', { node: option.chance, property: 'percent' });
        }
    }
}
