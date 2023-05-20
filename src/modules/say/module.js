// MODULE SAY
const {CommandInfo} = require("../../utils/interaction")
const {Message} = require('../../utils/message')
const DI = require("../../discord_interface");

class Module {
    constructor(create_infos) {
        this.client = create_infos.client

        // Command declaration
        this.commands = [
            new CommandInfo('say', 'Fait parler bidibip')
                .add_text_option('message', 'Ce que bidibip dira pour vous', [], true)
                .set_member_only()
        ]
    }

    // When server command is executed
    async server_interaction(command) {
        if (command.match('say')) {
            await new Message()
                .set_channel(command.channel())
                .set_text(command.read('message'))
                .send()
            command.skip()
        }
    }
}

module.exports = {Module}