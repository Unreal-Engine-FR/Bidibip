// MODULE HELP
const {CommandInfo, Message, Embed} = require("../../discord_interface");

MODULE_MANAGER = require("../../module_manager").get()

class Module {
    constructor(create_infos) {
        this.client = create_infos.client

        this.commands = [
            new CommandInfo('help', 'Voir la liste des commandes disponibles')
        ]
    }

    // When server command is issued
    server_command(command) {
        if (command.match('help')) {
            const commands = MODULE_MANAGER.event_manager().get_commands()
            if (!commands)
                return

            const embed = new Embed()
                .set_title('Aide de Bidibip')
                .set_description('liste des commandes disponibles :')
            for (const command of commands)
                embed.add_field(command.name, command.description)

            const message = new Message()
                .set_channel(command.channel)
                .add_embed(embed)
                .set_client_only()

            command.reply(message)
        }
    }
}

module.exports = {Module}