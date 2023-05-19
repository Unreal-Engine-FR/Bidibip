// MODULE SAY
const {CommandInfo, Message} = require("../../discord_interface");

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
    server_command(command) {
        if (command.match('say')) {
            this.client.say(
                new Message()
                    .set_channel(command.channel)
                    .set_text(command.option_value('message'))
            )
            command.skip()
        }
    }
}

module.exports = {Module}