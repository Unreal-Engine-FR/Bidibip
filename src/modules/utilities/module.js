// MODULE UTILITIES
const {CommandInfo, Message, Embed} = require("../../discord_interface");
const MODULE_MANAGER = require('../../module_manager').get()

class Module {
    constructor(create_infos) {
        this.client = create_infos.client

        // Command declaration
        this.commands = [
            new CommandInfo('modules_infos', 'Informations sur les modules')
                .set_admin_only(),
            new CommandInfo('set_module_enabled', 'Active ou desactive un module')
                .add_text_option('nom', 'nom du module')
                .add_bool_option('enabled', 'Nouvel etat du module')
                .set_admin_only(),
            new CommandInfo('load_module', 'Charge un module')
                .add_text_option('nom', 'nom du module')
                .set_admin_only(),
            new CommandInfo('unload_module', 'Decharge un module')
                .add_text_option('nom', 'nom du module')
                .set_admin_only()
        ]
    }

    server_command(command) {
        if (command.match('modules_infos')) {
            const embed = new Embed()
                .set_title('modules')
                .set_description('liste des modules disponibles')

            for (const module of MODULE_MANAGER.all_modules_info()) {
                embed.add_field(module.name, `chargé : ${module.loaded ? ':white_check_mark:' : ':x:'}\tactivé : ${module.enabled ? ':white_check_mark:' : ':x:'}`)
            }

            command.reply(
                new Message()
                    .add_embed(embed)
                    .set_client_only()
            )
        }
        if (command.match('set_module_enabled')) {
            if (command.option_value('enabled') === true)
                MODULE_MANAGER.start(command.option_value('nom'))
            else
                MODULE_MANAGER.stop(command.option_value('nom'))
            command.skip()
        }
        if (command.match('load_module')) {
            MODULE_MANAGER.load_module(command.option_value('nom'))
            command.skip()
        }
        if (command.match('unload_module')) {
            MODULE_MANAGER.unload_module(command.option_value('nom'))
            command.skip()
        }
    }
}

module.exports = {Module}