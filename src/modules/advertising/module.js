// MODULE ADVERTISING
const {CommandInfo} = require("../../discord_interface");

MODULE_MANAGER = require("../../module_manager").get()

class Module {
    constructor(create_infos) {
        this.client = create_infos.client

        this.commands = [
            new CommandInfo('freelance', 'Ajouter une annonce de freelance'),
            new CommandInfo('paid', 'Ajouter une annonce payante'),
            new CommandInfo('unpaid', 'Ajouter une annonce bénévole'),
        ]
    }

    // When server command is issued
    server_command(command) {
        command.skip()
        if (command.match('paid')) {
        }
    }
}

module.exports = {Module}